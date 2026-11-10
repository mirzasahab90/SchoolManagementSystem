"""End-to-end checks for Attendance.

Usage: python tests/e2e/attendance_e2e.py <group> [<group> ...]
Runs against the local dev API; groups clean up what they create where the API allows it.
"""
import datetime as _dt
import re as _re
import sys
import threading as _threading

from sms_client import (admin_token, call, check, cleanup, data, download, login, make_user, summary, track,
                        wait_for_api, _created)
import random as _random
from academics_e2e import actions, campus_admin, demo_campuses, menu_keys, role_detail, session_body, sessions_list

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def iso(d):
    return d.isoformat()


def today():
    return _dt.datetime.now(_dt.UTC).date()


# ------------------------------------------------------------------ calendar helpers

def calendar(token, campus):
    return data(call("GET", "/calendar", token=token, campus=campus))


def set_weekly_off(token, campus, days):
    return call("PUT", "/calendar/weekly-off", {"days": days}, token=token, campus=campus)


def holiday_body(name, start, end, all_campuses=False):
    return {"name": name, "startDate": iso(start), "endDate": iso(end), "allCampuses": all_campuses}


def add_holiday(token, campus, name, start, end, all_campuses=False):
    s, r = call("POST", "/calendar/holidays", holiday_body(name, start, end, all_campuses), token=token, campus=campus)
    if s == 200:
        track("holidays", r["data"]["id"])
    return s, r


def working_days_back(token, campus, count, start_offset=0):
    """`count` working dates (newest first) on or before today - start_offset, per the campus calendar."""
    cal = calendar(token, campus)
    off = set(cal["weeklyOff"])
    hol = [(h["startDate"], h["endDate"]) for h in cal["holidays"]]
    out, d = [], today() - _dt.timedelta(days=start_offset)
    while len(out) < count:
        name = d.strftime("%A")
        if name not in off and not any(s <= iso(d) <= e for s, e in hol):
            out.append(d)
        d -= _dt.timedelta(days=1)
    return out


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    me = data(call("GET", "/auth/me", token=hq))
    check("menu has Attendance after Staff", menu_keys(me) == ["dashboard", "campuses", "academics", "students-menu", "staff-menu",
                                                              "attendance-menu", "exams-menu", "settings"], menu_keys(me))
    grp = next((m for m in me["menu"] if m["key"] == "attendance-menu"), None)
    check("attendance children", grp and [c["key"] for c in grp["children"]] == ["attendance", "attendance-reports", "staff-attendance", "calendar"], grp)
    full, view = {"view", "add", "edit", "delete"}, {"view"}
    keys = ("attendance", "attendance-reports", "staff-attendance", "calendar")
    expected = {
        "Principal": {"attendance": full, "attendance-reports": view, "staff-attendance": full, "calendar": full},
        "CampusAdmin": {k: full for k in keys},
        "Teacher": {"attendance": {"view", "add", "edit"}, "attendance-reports": view, "staff-attendance": set(), "calendar": set()},
        "Clerk": {"attendance": view, "attendance-reports": view, "staff-attendance": {"view", "add", "edit"}, "calendar": view},
        "Accountant": {k: set() for k in keys},
    }
    for role, want in expected.items():
        d = role_detail(hq, role)
        got = {k: actions(d, k) for k in keys}
        check(f"{role} attendance grants", got == want, got)


# ------------------------------------------------------------------ calendar

@group
def calendar_group(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    cal = calendar(ca, a)
    check("default weekly off is Sunday", cal["weeklyOff"] == ["Sunday"], cal)
    s, r = set_weekly_off(ca, a, ["Friday", "Sunday"])
    check("set weekly off", s == 200 and calendar(ca, a)["weeklyOff"] == ["Friday", "Sunday"], (s, r))
    set_weekly_off(ca, a, ["Sunday"])
    start = today() + _dt.timedelta(days=40)
    s, r = add_holiday(ca, a, "QA Holiday", start, start + _dt.timedelta(days=2))
    ok = check("add campus holiday", s == 200 and r["data"]["name"] == "QA Holiday" and r["data"].get("campusId") == a, (s, r))
    if ok:
        hid = r["data"]["id"]
        s, r = call("PUT", f"/calendar/holidays/{hid}", holiday_body("QA Holiday 2", start, start), token=ca, campus=a)
        check("edit holiday", s == 200 and r["data"]["name"] == "QA Holiday 2", (s, r))
        check("listed for the campus", any(h["id"] == hid for h in calendar(ca, a)["holidays"]))
        check("not listed on another campus", not any(h["id"] == hid for h in calendar(hq, b)["holidays"]))
        s, _ = call("DELETE", f"/calendar/holidays/{hid}", token=ca, campus=a)
        check("delete holiday", s == 200, s)
    s, r = add_holiday(ca, a, "QA Bad", start, start - _dt.timedelta(days=1))
    check("end before start -> 400", s == 400, (s, r))
    s, r = add_holiday(ca, a, "QA Long", start, start + _dt.timedelta(days=60))
    check("61 days -> 400", s == 400, (s, r))
    s, r = add_holiday(ca, a, "QA All", start, start, all_campuses=True)
    check("campus admin all-campus holiday -> 403", s == 403, (s, r))
    s, r = add_holiday(hq, None, "QA All", start, start, all_campuses=True)
    ok = check("Main Campus all-campus holiday", s == 200 and r["data"].get("campusId") is None, (s, r))
    if ok:
        hid = r["data"]["id"]
        check("all-campus holiday visible on both campuses", any(h["id"] == hid for h in calendar(ca, a)["holidays"])
              and any(h["id"] == hid for h in calendar(hq, b)["holidays"]))
        s, _ = call("DELETE", f"/calendar/holidays/{hid}", token=ca, campus=a)
        check("campus admin cannot delete an all-campus holiday", s == 403, s)
    s, r = call("GET", "/calendar", token=hq)
    check("calendar needs a working campus", s == 400, (s, r))


GROUPS["calendar"] = GROUPS.pop("calendar-group")


# ------------------------------------------------------------------ marking helpers

def rnd(n):
    return "".join(_random.choice("0123456789") for _ in range(n))


def qa_section(token, campus, class_code="G1"):
    """A fresh section in the campus's current session, tracked for cleanup."""
    rows = data(call("GET", "/sections", token=token, campus=campus))
    class_id = next(x["classId"] for x in rows if x["classCode"] == class_code)
    s, r = call("POST", "/sections", {"classId": class_id, "academicSessionId": None, "name": "Q" + rnd(3), "shift": "Morning",
                                     "capacity": None, "room": None, "isActive": True}, token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"qa_section: {s} {r}")
    return track("sections", r["data"]["id"]), r["data"]


def admit_on(token, campus, section_id, admitted):
    from students_e2e import admit
    s, r = admit(token, campus, section_id, admissionDate=iso(admitted))
    if s != 200:
        raise AssertionError(f"admit: {s} {r}")
    return r["data"]["student"]["id"]


def sheet(token, campus, section_id, d):
    return call("GET", f"/attendance/sheet?sectionId={section_id}&date={iso(d)}", token=token, campus=campus)


def save(token, campus, section_id, d, statuses, notes=None):
    entries = [{"studentId": sid, "status": st, "note": (notes or {}).get(sid)} for sid, st in statuses.items()]
    return call("PUT", "/attendance/sheet", {"sectionId": section_id, "date": iso(d), "entries": entries}, token=token, campus=campus)


def all_present(token, campus, section_id, d):
    s, r = sheet(token, campus, section_id, d)
    return {row["studentId"]: "Present" for row in r["data"]["rows"]}


def setup_section(hq, campus, ca, n=3, admitted_days_ago=40):
    sec, sdto = qa_section(ca, campus)
    ids = [admit_on(ca, campus, sec, today() - _dt.timedelta(days=admitted_days_ago)) for _ in range(n)]
    return sec, sdto, ids


# ------------------------------------------------------------------ marking

@group
def marking(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, _, (s1, s2, s3) = setup_section(hq, a, ca)
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    s, r = sheet(ca, a, sec, d)
    ok = check("unmarked sheet: everyone Present, not marked, editable", s == 200 and not r["data"]["marked"] and r["data"]["canEdit"]
               and {x["studentId"] for x in r["data"]["rows"]} == {s1, s2, s3} and all(x["status"] == "Present" for x in r["data"]["rows"]), (s, r))
    if not ok:
        return
    s, r = save(ca, a, sec, d, {s1: "Present", s2: "Absent", s3: "Late"}, {s2: "sick"})
    check("save", s == 200, (s, r))
    s, r = sheet(ca, a, sec, d)
    rows = {x["studentId"]: x for x in r["data"]["rows"]}
    check("saved statuses come back", r["data"]["marked"] and rows[s2]["status"] == "Absent" and rows[s2].get("note") == "sick"
          and rows[s3]["status"] == "Late" and r["data"].get("markedBy"), r["data"])
    save(ca, a, sec, d, {s1: "Present", s2: "Leave", s3: "Late"})
    s, r = sheet(ca, a, sec, d)
    check("re-save edits the same day", {x["studentId"]: x["status"] for x in r["data"]["rows"]}[s2] == "Leave", r["data"]["rows"])

    # roster_follows_enrollment_dates
    late = admit_on(ca, a, sec, today())
    s, r = sheet(ca, a, sec, d)
    check("roster_follows_enrollment_dates: late joiner not on an earlier date", late not in {x["studentId"] for x in r["data"]["rows"]}, r["data"]["rows"])
    s, r = save(ca, a, sec, d, {s1: "Present", s2: "Present", s3: "Present", late: "Present"})
    check("roster_follows_enrollment_dates: saving the late joiner -> 400", s == 400 and "does not match" in (r.get("message") or ""), (s, r))
    s, r = save(ca, a, sec, d, {s1: "Present", s2: "Present"})
    check("missing a student -> 400", s == 400, (s, r))
    s, r = call("PUT", "/attendance/sheet", {"sectionId": sec, "date": iso(d), "entries": [
        {"studentId": s1, "status": "Present"}, {"studentId": s1, "status": "Absent"}, {"studentId": s2, "status": "Present"},
        {"studentId": s3, "status": "Present"}]}, token=ca, campus=a)
    check("duplicate student -> 400", s == 400, (s, r))
    sec2, _ = qa_section(ca, a)
    s, r = call("POST", f"/students/{s3}/change-section", {"sectionId": sec2, "rollNo": None, "reason": "QA"}, token=ca, campus=a)
    if s == 200:
        on_new = {x["studentId"] for x in sheet(ca, a, sec2, d)[1]["data"]["rows"]}
        on_old = {x["studentId"] for x in sheet(ca, a, sec, d)[1]["data"]["rows"]}
        check("moved student stays on the old section's sheet for a day marked there, not on the new one", s3 in on_old and s3 not in on_new, (on_new, on_old))
        call("POST", f"/students/{s3}/change-section", {"sectionId": sec, "rollNo": None, "reason": "QA"}, token=ca, campus=a)

    # concurrent_first_save_one_day
    d2 = working_days_back(ca, a, 1, start_offset=(today() - d).days + 1)[0]
    roster = all_present(ca, a, sec, d2)
    codes = []

    def go():
        codes.append(save(ca, a, sec, d2, roster)[0])

    th = [_threading.Thread(target=go) for _ in range(2)]
    for x in th:
        x.start()
    for x in th:
        x.join()
    check("concurrent_first_save_one_day", 200 in codes and set(codes) <= {200, 409} and sheet(ca, a, sec, d2)[1]["data"]["marked"], codes)

    s, r = call("GET", "/attendance/my-sections", token=ca, campus=a)
    check("my-sections lists the QA section for an admin", s == 200 and any(x["sectionId"] == sec for x in r["data"]), (s, r))


# ------------------------------------------------------------------ rules

@group
def rules(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, _, ids = setup_section(hq, a, ca, n=1, admitted_days_ago=60)
    st = {ids[0]: "Present"}
    s, r = save(ca, a, sec, today() + _dt.timedelta(days=1), st)
    check("future date -> 400", s == 400 and r.get("message") == "You cannot mark a future date.", (s, r))
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    day = d.strftime("%A")
    set_weekly_off(ca, a, [day, "Sunday"])
    s, r = save(ca, a, sec, d, st)
    check("weekly off -> 400", s == 400 and r.get("message") == day + " is a weekly off.", (s, r))
    s, r = sheet(ca, a, sec, d)
    check("sheet on a weekly off is locked", s == 200 and not r["data"]["isWorkingDay"] and not r["data"]["canEdit"], r)
    set_weekly_off(ca, a, ["Sunday"])
    add_holiday(ca, a, "QA Holiday", d, d)
    s, r = save(ca, a, sec, d, st)
    check("holiday -> 400 with the name", s == 400 and "is a holiday: QA Holiday." in (r.get("message") or ""), (s, r))
    for h in list(_created["holidays"]):
        call("DELETE", f"/calendar/holidays/{h}", token=ca, campus=a)
        _created["holidays"].remove(h)
    lst = sessions_list(ca, a)
    cur = next(x for x in lst["items"] if x["id"] == lst["effectiveCurrentId"])
    before = _dt.date.fromisoformat(cur["startDate"]) - _dt.timedelta(days=3)
    while before.strftime("%A") == "Sunday":
        before -= _dt.timedelta(days=1)
    s, r = save(ca, a, sec, before, st)
    check("date before the session -> 400", s == 400, (s, r))


# ------------------------------------------------------------------ class teacher

def teacher_with_login(hq, ca, campus):
    from onboarding_e2e import make_staff, login_body
    sid, _ = make_staff(ca, campus, category="Teaching", designation="Teacher")
    body = login_body(role="Teacher", hq=hq)
    s, r = call("POST", f"/staff/{sid}/account", body, token=ca, campus=campus)
    track("users", r["data"]["account"]["userId"])
    tok = login(body["email"], body["password"])["tokens"]["accessToken"]
    newpw = body["password"] + "x9"
    s, _ = call("POST", "/auth/change-password", {"currentPassword": body["password"], "newPassword": newpw}, token=tok)
    if s == 200:
        tok = login(body["email"], newpw)["tokens"]["accessToken"]
    return sid, tok


@group
def class_teacher(hq):
    from onboarding_e2e import retire_staff
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, _, ids = setup_section(hq, a, ca, n=2, admitted_days_ago=40)
    other, _, other_ids = setup_section(hq, a, ca, n=1, admitted_days_ago=40)
    t1, tok1 = teacher_with_login(hq, ca, a)
    t2, tok2 = teacher_with_login(hq, ca, a)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": t1}, token=ca, campus=a)
    s, r = call("GET", "/attendance/my-sections", token=tok1, campus=a)
    check("teacher my-sections = own class-teacher section only", s == 200 and [x["sectionId"] for x in r["data"]] == [sec]
          and r["data"][0]["isClassTeacher"], (s, r))
    st = {i: "Present" for i in ids}
    days = working_days_back(ca, a, 6)
    within = [d for d in days if (today() - d).days <= 2]
    older = [d for d in days if (today() - d).days >= 3]
    for d in within:
        s, r = save(tok1, a, sec, d, st)
        check(f"class teacher marks {iso(d)} (within 2 days)", s == 200, (s, r))
    if older:
        s, r = save(tok1, a, sec, older[0], st)
        check("class teacher 3+ days back -> 403", s == 403 and r.get("message") == "You can mark this section for today and the last 2 days only.", (s, r))
        s, r = sheet(tok1, a, sec, older[0])
        check("sheet shows the lock reason", s == 200 and not r["data"]["canEdit"] and r["data"].get("editLockedReason"), r)
        s, r = save(ca, a, sec, older[0], st)
        check("admin may mark an older date", s == 200, (s, r))
    d0 = days[0]
    s, r = save(tok1, a, other, d0, {other_ids[0]: "Present"})
    check("teacher on another section -> 403", s == 403 and r.get("message") == "Only the class teacher or an admin can mark this section.", (s, r))
    s, r = save(ca, a, other, d0, {other_ids[0]: "Present"})
    check("section without class teacher: admin 200", s == 200, (s, r))
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": t2}, token=ca, campus=a)
    s, _ = save(tok1, a, sec, d0, st)
    check("class_teacher_swap: old teacher -> 403", s == 403, s)
    s, r = save(tok2, a, sec, d0, st)
    check("class_teacher_swap: new teacher -> 200", s == 200, (s, r))
    _, ce, cp = make_user(hq, campus_ids=[a], role_names=["Clerk"])
    clerk = login(ce, cp)["tokens"]["accessToken"]
    s, _ = save(clerk, a, sec, d0, st)
    check("clerk (view only) cannot save", s == 403, s)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": None}, token=ca, campus=a)
    retire_staff(hq)


# ------------------------------------------------------------------ reports

@group
def reports(hq):
    from onboarding_e2e import retire_staff
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, sdto, (sa, sb) = setup_section(hq, a, ca, n=2, admitted_days_ago=40)
    days = [d for d in working_days_back(ca, a, 6, start_offset=1)]
    month = days[0].strftime("%Y-%m")
    days = [d for d in days if d.strftime("%Y-%m") == month][:3]
    if len(days) < 3:
        check("three working days in one month available", False, days)
        return
    d3, d2, d1 = days  # newest first
    plan = {d1: {sa: "Present", sb: "Present"}, d2: {sa: "Present", sb: "Late"}, d3: {sa: "Absent", sb: "Leave"}}
    for d, st in plan.items():
        save(ca, a, sec, d, st)
    s, r = call("GET", f"/attendance/monthly?sectionId={sec}&month={month}", token=ca, campus=a)
    ok = check("monthly sheet", s == 200 and len(r["data"]["students"]) == 2, (s, r))
    if not ok:
        return
    m = {x["studentId"]: x for x in r["data"]["students"]}
    dayidx = {x["date"]: i for i, x in enumerate(r["data"]["days"])}
    check("monthly cells match", [m[sa]["cells"][dayidx[iso(d)]] for d in (d1, d2, d3)] == ["P", "P", "A"]
          and [m[sb]["cells"][dayidx[iso(d)]] for d in (d1, d2, d3)] == ["P", "L", "Lv"], (m[sa]["cells"], m[sb]["cells"]))
    check("percent formula: P,P,A -> 66.7 and P,L,Lv -> 100.0", m[sa]["pct"] == 66.7 and m[sb]["pct"] == 100.0, (m[sa]["pct"], m[sb]["pct"]))
    check("totals", (m[sa]["present"], m[sa]["absent"]) == (2, 1) and (m[sb]["late"], m[sb]["leave"]) == (1, 1), (m[sa], m[sb]))
    check("weekly offs carry their kind", any(x["kind"] == "WeeklyOff" for x in r["data"]["days"]), r["data"]["days"][:8])

    # holiday_after_marking_excluded
    add_holiday(ca, a, "QA Holiday", d3, d3)
    r = data(call("GET", f"/attendance/monthly?sectionId={sec}&month={month}", token=ca, campus=a))
    m = {x["studentId"]: x for x in r["students"]}
    kind = next(x["kind"] for x in r["days"] if x["date"] == iso(d3))
    check("holiday_after_marking_excluded", kind == "Holiday" and m[sa]["pct"] == 100.0, (kind, m[sa]["pct"]))
    for h in list(_created["holidays"]):
        call("DELETE", f"/calendar/holidays/{h}", token=ca, campus=a)
        _created["holidays"].remove(h)

    s, body, headers = download(f"/attendance/monthly.csv?sectionId={sec}&month={month}", ca, a)
    text = body.decode("utf-8-sig") if s == 200 else ""
    check("monthly CSV", s == 200 and "text/csv" in headers.get("Content-Type", "") and text.startswith("Roll,Name,")
          and len(text.strip().splitlines()) == 3, (s, text[:120]))

    t = today()
    is_working = working_days_back(ca, a, 1)[0] == t
    if is_working:
        save(ca, a, sec, t, {sa: "Absent", sb: "Present"})
    s, r = call("GET", "/attendance/today", token=ca, campus=a)
    ok = check("today report", s == 200, (s, r))
    if ok and is_working:
        row = next((x for x in r["data"]["sections"] if x["sectionId"] == sec), None)
        check("today lists the section with counts", row and row["marked"] and row["absent"] == 1, row)
        check("today absentees include the absent student", any(x["studentId"] == sa for x in r["data"]["absentees"]), r["data"]["absentees"])
        other, _ = qa_section(ca, a)
        r = data(call("GET", "/attendance/today", token=ca, campus=a))
        check("unmarked section listed as not marked", any(x["sectionId"] == other and not x["marked"] for x in r["sections"]))

    s, r = call("GET", f"/attendance/low?threshold=75&sectionId={sec}", token=ca, campus=a)
    ids = [x["studentId"] for x in r["data"]] if s == 200 else []
    check("low attendance includes the low student only", s == 200 and sa in ids and sb not in ids, (s, r))

    s, r = call("GET", f"/students/{sa}/attendance", token=ca, campus=a)
    check("student record", s == 200 and r["data"]["pct"] is not None and r["data"]["absent"] >= 1
          and any(x["date"] == iso(d3) for x in r["data"]["absences"]) and r["data"]["months"], (s, r))

    # teacher sees own sections only
    t1, tok1 = teacher_with_login(hq, ca, a)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": t1}, token=ca, campus=a)
    s, r = call("GET", "/attendance/today", token=tok1, campus=a)
    check("teacher today shows only own section", s == 200 and [x["sectionId"] for x in r["data"]["sections"]] == [sec], (s, r))
    other2, _ = qa_section(ca, a)
    s, _ = call("GET", f"/attendance/monthly?sectionId={other2}&month={month}", token=tok1, campus=a)
    check("teacher monthly of another section -> 403", s == 403, s)
    s, _ = call("GET", f"/attendance/monthly?sectionId={sec}&month={month}", token=tok1, campus=a)
    check("teacher monthly of own section -> 200", s == 200, s)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": None}, token=ca, campus=a)
    retire_staff(hq)


@group
def dashboard_attendance(hq):
    a, _ = demo_campuses(hq)
    kpis = data(call("GET", "/dashboard", token=hq, campus=a))["kpis"]
    k = next((x for x in kpis if x["id"] == "attendance"), None)
    check("attendance KPI", k and (k["value"].endswith("%") or k["value"] == "—")
          and _re.fullmatch(r"\d+ sections? not marked", k.get("hint") or "") and k.get("href") == "/admin/attendance/reports", k)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Accountant"])
    tok = login(e, p)["tokens"]["accessToken"]
    kpis = data(call("GET", "/dashboard", token=tok, campus=a))["kpis"]
    check("no attendance KPI for an Accountant", not any(x["id"] == "attendance" for x in kpis), [x["id"] for x in kpis])


# ------------------------------------------------------------------ staff attendance

def staff_sheet(token, campus, d):
    return call("GET", f"/staff-attendance?date={iso(d)}", token=token, campus=campus)


def staff_save(token, campus, d, rows):
    return call("PUT", "/staff-attendance", {"date": iso(d), "entries": rows}, token=token, campus=campus)


@group
def staff_attendance(hq):
    from onboarding_e2e import make_staff, retire_staff, set_status
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    x1, _ = make_staff(ca, a, joiningDate=iso(today() - _dt.timedelta(days=60)))
    x2, _ = make_staff(ca, a, joiningDate=iso(today() - _dt.timedelta(days=60)))
    future, _ = make_staff(ca, a, joiningDate=iso(today() + _dt.timedelta(days=1)))
    left, _ = make_staff(ca, a, joiningDate=iso(today() - _dt.timedelta(days=60)))
    s, r = staff_sheet(ca, a, d)
    ids = {x["staffId"] for x in r["data"]["rows"]} if s == 200 else set()
    check("staff sheet lists current staff", s == 200 and {x1, x2, left} <= ids, (s, r))
    check("staff_roster_by_dates: joining tomorrow not listed", future not in ids, ids)
    s, r = staff_save(ca, a, d, [{"staffId": x1, "status": "Present", "checkIn": "08:05:00", "note": None},
                                 {"staffId": x2, "status": "HalfDay", "checkIn": None, "note": "doctor"}])
    check("save staff attendance", s == 200, (s, r))
    rows = {x["staffId"]: x for x in staff_sheet(ca, a, d)[1]["data"]["rows"]}
    check("saved rows come back", rows[x1]["status"] == "Present" and (rows[x1].get("checkIn") or "").startswith("08:05")
          and rows[x2]["status"] == "HalfDay" and rows[x2]["marked"] and not rows[left].get("marked"), rows)
    staff_save(ca, a, d, [{"staffId": x1, "status": "Late", "checkIn": "09:10:00", "note": None}])
    rows = {x["staffId"]: x for x in staff_sheet(ca, a, d)[1]["data"]["rows"]}
    check("re-save upserts one row, leaves others", rows[x1]["status"] == "Late" and rows[x2]["status"] == "HalfDay", rows)
    s, r = staff_save(ca, a, d, [{"staffId": future, "status": "Present", "checkIn": None, "note": None}])
    check("staff_roster_by_dates: saving a not-yet-joined member -> 400", s == 400, (s, r))
    set_status(ca, a, left, "Resigned", iso(today() - _dt.timedelta(days=1)), "left")
    s, r = staff_sheet(ca, a, today())
    check("staff_roster_by_dates: member who left yesterday not listed today", s != 200 or left not in {x["staffId"] for x in r["data"]["rows"]}, r)
    s, r = staff_save(ca, a, today() + _dt.timedelta(days=1), [{"staffId": x1, "status": "Present", "checkIn": None, "note": None}])
    check("future date -> 400", s == 400, (s, r))
    add_holiday(ca, a, "QA Holiday", d, d)
    s, r = staff_save(ca, a, d, [{"staffId": x1, "status": "Present", "checkIn": None, "note": None}])
    check("holiday -> 400", s == 400, (s, r))
    for h in list(_created["holidays"]):
        call("DELETE", f"/calendar/holidays/{h}", token=ca, campus=a)
        _created["holidays"].remove(h)
    month = d.strftime("%Y-%m")
    s, r = call("GET", f"/staff-attendance/monthly?month={month}", token=ca, campus=a)
    row = next((x for x in r["data"]["staff"] if x["staffId"] == x2), None) if s == 200 else None
    check("staff monthly shows H and half-day pct", row and "H" in row["cells"] and row["halfDay"] == 1 and row["pct"] == 50.0, row)
    _, ce, cp = make_user(hq, campus_ids=[a], role_names=["Clerk"])
    clerk = login(ce, cp)["tokens"]["accessToken"]
    s, _ = staff_save(clerk, a, d, [{"staffId": x1, "status": "Present", "checkIn": None, "note": None}])
    check("clerk can save staff attendance", s == 200, s)
    _, te, tp = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    teacher = login(te, tp)["tokens"]["accessToken"]
    s, _ = staff_sheet(teacher, a, d)
    check("teacher -> 403 on staff attendance", s == 403, s)
    retire_staff(hq)


# ------------------------------------------------------------------ isolation & permissions

@group
def attendance_isolation(hq):
    from onboarding_e2e import make_staff, retire_staff
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    d = working_days_back(hq, b, 1, start_offset=1)[0]
    bsec, _, (bs,) = setup_section(hq, b, hq, n=1, admitted_days_ago=40)
    save(hq, b, bsec, d, {bs: "Absent"})
    bstaff, _ = make_staff(hq, b, joiningDate=iso(today() - _dt.timedelta(days=60)))
    staff_save(hq, b, d, [{"staffId": bstaff, "status": "Present", "checkIn": None, "note": None}])
    s, r = add_holiday(hq, b, "QA B Holiday", today() + _dt.timedelta(days=50), today() + _dt.timedelta(days=50))
    bh = r["data"]["id"] if s == 200 else 0
    month = d.strftime("%Y-%m")
    attempts = [
        ("sheet GET", sheet(ca, a, bsec, d)[0]),
        ("sheet PUT", save(ca, a, bsec, d, {bs: "Present"})[0]),
        ("monthly", call("GET", f"/attendance/monthly?sectionId={bsec}&month={month}", token=ca, campus=a)[0]),
        ("monthly csv", download(f"/attendance/monthly.csv?sectionId={bsec}&month={month}", ca, a)[0]),
        ("student record", call("GET", f"/students/{bs}/attendance", token=ca, campus=a)[0]),
        ("holiday edit", call("PUT", f"/calendar/holidays/{bh}", holiday_body("x", today(), today()), token=ca, campus=a)[0]),
        ("holiday delete", call("DELETE", f"/calendar/holidays/{bh}", token=ca, campus=a)[0]),
    ]
    check("every cross-campus attendance action -> 404", all(c == 404 for _, c in attempts), attempts)
    s, r = call("GET", "/attendance/today", token=ca, campus=a)
    check("today shows no campus-B sections", s == 200 and not any(x["sectionId"] == bsec for x in r["data"]["sections"]), s)
    s, r = call("GET", "/attendance/low?threshold=101", token=ca, campus=a)
    check("low shows no campus-B students", s == 400 or not any(x["studentId"] == bs for x in r["data"]), s)
    s, r = staff_sheet(ca, a, d)
    check("staff sheet shows no campus-B staff", s == 200 and not any(x["staffId"] == bstaff for x in r["data"]["rows"]), s)
    s, r = staff_save(ca, a, d, [{"staffId": bstaff, "status": "Absent", "checkIn": None, "note": None}])
    check("saving a campus-B staff member -> 400", s == 400, (s, r))
    after = {x["staffId"]: x for x in staff_sheet(hq, b, d)[1]["data"]["rows"]}
    check("campus-B data unchanged", after[bstaff]["status"] == "Present"
          and {x["studentId"]: x["status"] for x in sheet(hq, b, bsec, d)[1]["data"]["rows"]}[bs] == "Absent", after.get(bstaff))
    retire_staff(hq)


@group
def attendance_permissions(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, _, (s1,) = setup_section(hq, a, ca, n=1, admitted_days_ago=40)
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    month = d.strftime("%Y-%m")
    _, ae, ap = make_user(hq, campus_ids=[a], role_names=["Accountant"])
    acc = login(ae, ap)["tokens"]["accessToken"]
    codes = [sheet(acc, a, sec, d)[0], call("GET", "/attendance/today", token=acc, campus=a)[0],
             call("GET", f"/attendance/monthly?sectionId={sec}&month={month}", token=acc, campus=a)[0],
             staff_sheet(acc, a, d)[0], set_weekly_off(acc, a, ["Sunday"])[0]]
    check("Accountant -> 403 on attendance", all(c == 403 for c in codes), codes)
    _, te, tp = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    teacher = login(te, tp)["tokens"]["accessToken"]
    codes = [staff_sheet(teacher, a, d)[0], set_weekly_off(teacher, a, ["Sunday"])[0],
             add_holiday(teacher, a, "x", today() + _dt.timedelta(days=90), today() + _dt.timedelta(days=90))[0]]
    check("Teacher -> 403 on staff attendance and calendar edits", all(c == 403 for c in codes), codes)
    _, pe, pp = make_user(hq, campus_ids=[a], role_names=["Principal"])
    pr = login(pe, pp)["tokens"]["accessToken"]
    codes = [save(pr, a, sec, d, {s1: "Present"})[0], call("GET", "/attendance/today", token=pr, campus=a)[0],
             staff_sheet(pr, a, d)[0], set_weekly_off(pr, a, ["Sunday"])[0]]
    check("Principal -> 200 everywhere", all(c == 200 for c in codes), codes)


# ------------------------------------------------------------------ review fixes

@group
def attendance_review_fixes(hq):
    from onboarding_e2e import retire_staff
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    secA, _, (s, other) = setup_section(hq, a, ca, n=2, admitted_days_ago=40)
    secB, _, (b1,) = setup_section(hq, a, ca, n=1, admitted_days_ago=40)
    d1, d2 = working_days_back(ca, a, 2, start_offset=1)  # d1 newer
    save(ca, a, secA, d1, {s: "Absent", other: "Present"})
    save(ca, a, secA, d2, {s: "Present", other: "Present"})
    save(ca, a, secB, d1, {b1: "Present"})
    call("POST", f"/students/{s}/change-section", {"sectionId": secB, "rollNo": None, "reason": "QA move"}, token=ca, campus=a)

    # moved_student_keeps_history: correcting A's day after the move keeps the moved student's entry
    s_rows = sheet(ca, a, secA, d1)[1]["data"]["rows"]
    # the UI re-sends every row with its current status; only the other student is corrected
    st, r = save(ca, a, secA, d1, {x["studentId"]: ("Late" if x["studentId"] == other else x["status"]) for x in s_rows})
    rec = data(call("GET", f"/students/{s}/attendance", token=ca, campus=a))
    check("moved_student_keeps_history: absence survives a re-save of the old section", st == 200 and rec["absent"] == 1, (st, rec))
    # moved_student_not_double_counted: B's past day does not list the student who was marked in A that day
    b_rows = {x["studentId"] for x in sheet(ca, a, secB, d1)[1]["data"]["rows"]}
    check("moved_student_not_double_counted: B's past sheet omits the student marked in A", s not in b_rows, b_rows)
    save(ca, a, secB, d1, {x: "Present" for x in b_rows})
    rec = data(call("GET", f"/students/{s}/attendance", token=ca, campus=a))
    check("moved_student_not_double_counted: one entry per day", rec["workingDaysMarked"] == 2, rec)

    # weekly_off_change_keeps_history
    month = d1.strftime("%Y-%m")
    before = next(x for x in data(call("GET", f"/attendance/monthly?sectionId={secA}&month={month}", token=ca, campus=a))["students"] if x["studentId"] == other)
    set_weekly_off(ca, a, [d1.strftime("%A"), "Sunday"])
    m = data(call("GET", f"/attendance/monthly?sectionId={secA}&month={month}", token=ca, campus=a))
    after = next(x for x in m["students"] if x["studentId"] == other)
    kind = next(x["kind"] for x in m["days"] if x["date"] == iso(d1))
    set_weekly_off(ca, a, ["Sunday"])
    check("weekly_off_change_keeps_history: marked day still counts", after["present"] == before["present"] and kind == "Working", (before, after, kind))

    # clerk_reports_whole_campus
    _, ce, cp = make_user(hq, campus_ids=[a], role_names=["Clerk"])
    clerk = login(ce, cp)["tokens"]["accessToken"]
    s1, r1 = call("GET", "/attendance/today", token=clerk, campus=a)
    s2, _ = call("GET", f"/attendance/monthly?sectionId={secA}&month={month}", token=clerk, campus=a)
    check("clerk_reports_whole_campus", s1 == 200 and any(x["sectionId"] == secA for x in r1["data"]["sections"]) and s2 == 200, (s1, s2))

    # teacher_scope_sheet_and_record
    t1, tok = teacher_with_login(hq, ca, a)
    call("PUT", f"/sections/{secB}/class-teacher", {"staffId": t1}, token=ca, campus=a)
    sA, _ = sheet(tok, a, secA, d1)
    sB, _ = sheet(tok, a, secB, d1)
    rA, _ = call("GET", f"/students/{other}/attendance", token=tok, campus=a)
    rB, _ = call("GET", f"/students/{b1}/attendance", token=tok, campus=a)
    check("teacher_scope_sheet_and_record: other section 403, own 200", (sA, sB, rA, rB) == (403, 200, 403, 200), (sA, sB, rA, rB))
    call("PUT", f"/sections/{secB}/class-teacher", {"staffId": None}, token=ca, campus=a)
    retire_staff(hq)


# ------------------------------------------------------------------ runner

def main(argv):
    names = argv or ["schema"]
    unknown = [n for n in names if n not in GROUPS]
    if unknown:
        sys.exit(f"unknown group(s): {unknown}; known: {sorted(GROUPS)}")
    wait_for_api()
    hq = admin_token()
    for name in names:
        print(f"\n== {name}")
        try:
            GROUPS[name](hq)
        except Exception as e:  # report and keep going; cleanup still runs
            check(f"{name}: setup", False, e)
        finally:
            cleanup(hq)
    return summary()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
