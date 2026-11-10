"""End-to-end checks for Staff Hiring (Part 1).

Usage: python tests/e2e/hiring_e2e.py <group> [<group> ...]
Runs against the local dev API; every group cleans up what it creates where the API allows it.
"""
import datetime as _dt
import random as _random
import re as _re
import sys
import threading as _threading
from urllib.parse import quote as _quote

from sms_client import (admin_token, call, check, cleanup, data, download, login, make_user, summary, track, upload,
                        wait_for_api)
from academics_e2e import actions, campus_admin, demo_campuses, menu_keys, role_detail

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def rand_digits(n):
    return "".join(_random.choice("0123456789") for _ in range(n))


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    me = data(call("GET", "/auth/me", token=hq))
    check("top menu includes Staff group",
          menu_keys(me) == ["dashboard", "campuses", "academics", "students-menu", "staff-menu", "attendance-menu", "exams-menu", "fees-menu", "settings"], menu_keys(me))
    grp = next((m for m in me["menu"] if m["key"] == "staff-menu"), None)
    check("staff group children", grp and [c["key"] for c in grp["children"]] == ["vacancies", "candidates", "staff"], grp)
    full = {"view", "add", "edit", "delete"}
    for role in ("Principal", "CampusAdmin"):
        d = role_detail(hq, role)
        check(f"{role} full on hiring modules", all(actions(d, k) == full for k in ("vacancies", "candidates", "staff")),
              {k: actions(d, k) for k in ("vacancies", "candidates", "staff")})
    for role in ("Teacher", "Clerk", "Accountant"):
        d = role_detail(hq, role)
        check(f"{role} has no hiring access", all(not actions(d, k) for k in ("vacancies", "candidates", "staff")),
              {k: actions(d, k) for k in ("vacancies", "candidates", "staff")})


# ------------------------------------------------------------------ helpers

PDF = b"%PDF-1.4\n%qa\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"
DOCX = b"PK\x03\x04" + b"\x14\x00\x06\x00" + b"\x00" * 64
DOC = bytes([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]) + b"\x00" * 64


def campus_code(hq, campus_id):
    return next(c["code"] for c in data(call("GET", "/campuses", token=hq)) if c["id"] == campus_id)


def vacancy_body(**over):
    body = {"title": "QA Maths Teacher " + rand_digits(4), "designation": "Teacher", "category": "Teaching", "positions": 2,
            "classIds": [], "subjectIds": [], "requirements": "B.Ed", "closingDate": None}
    body.update(over)
    return body


def make_vacancy(token, campus, **over):
    s, r = call("POST", "/vacancies", vacancy_body(**over), token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"make_vacancy: {s} {r}")
    return track("vacancies", r["data"]["id"]), r["data"]


def candidate_body(vacancy_id, **over):
    body = {"vacancyId": vacancy_id, "fullName": "Qa Candidate " + rand_digits(3), "cnic": rand_digits(13),
            "phone": "0300-" + rand_digits(7), "email": None, "qualification": "M.Sc Maths", "experienceYears": 3.5,
            "currentSalary": 40000, "expectedSalary": 55000, "source": "WalkIn"}
    body.update(over)
    return body


def make_candidate(token, campus, vacancy_id, **over):
    s, r = call("POST", "/candidates", candidate_body(vacancy_id, **over), token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"make_candidate: {s} {r}")
    return track("candidates", r["data"]["candidate"]["id"]), r["data"]


def move(token, campus, cid, stage, reason=None):
    return call("POST", f"/candidates/{cid}/move", {"stage": stage, "reason": reason}, token=token, campus=campus)


def soon(days=1):
    return (_dt.datetime.utcnow() + _dt.timedelta(days=days)).replace(microsecond=0).isoformat() + "Z"


def interviewer(hq, campus):
    uid, _, _ = make_user(hq, campus_ids=[campus], role_names=["Teacher"])
    return uid


def to_offer_accepted(token, campus, cid, interviewer_id, salary=60000):
    """Shortlist -> interview (Pass, 4) -> offer -> accept. Returns the profile."""
    move(token, campus, cid, "Shortlisted")
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "Demo", "scheduledAt": soon(), "interviewerUserId": interviewer_id},
                token=token, campus=campus)
    rid = r["data"]["interviews"][0]["id"]
    call("PUT", f"/candidates/{cid}/interviews/{rid}", {"name": "Demo", "scheduledAt": soon(), "interviewerUserId": interviewer_id,
                                                      "result": "Pass", "rating": 4, "notes": "good"}, token=token, campus=campus)
    call("POST", f"/candidates/{cid}/offer", {"designation": "Teacher", "employmentType": "Permanent", "offeredSalary": salary,
                                              "proposedJoiningDate": _dt.date.today().isoformat()}, token=token, campus=campus)
    return call("POST", f"/candidates/{cid}/offer/accept", token=token, campus=campus)


def hire_body(salary=61000):
    return {"joiningDate": _dt.date.today().isoformat(), "employmentType": "Permanent", "basicSalary": salary}


def retire_candidates(hq):
    """Withdraw tracked candidates so cleanup can delete them (hired ones stay, by design)."""
    from sms_client import _created
    for cid in list(_created["candidates"]):
        s, _ = move(hq, None, cid, "Withdrawn", "qa cleanup")
        if s == 400:  # hired: the candidate, its vacancy and the staff row stay by design
            vid = data(call("GET", f"/candidates/{cid}", token=hq))["candidate"]["vacancyId"]
            _created["candidates"].remove(cid)
            if vid in _created["vacancies"]:
                _created["vacancies"].remove(vid)


# ------------------------------------------------------------------ vacancies

@group
def vacancies(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    s, r = call("POST", "/vacancies", vacancy_body(), token=ca, campus=a)
    ok = check("create vacancy as campus admin", s == 200 and r["data"]["status"] == "Open"
               and r["data"]["counts"] == {"applied": 0, "shortlisted": 0, "interview": 0, "offered": 0, "hired": 0}
               and r["data"]["hired"] == 0, (s, r))
    if not ok:
        return
    vid = track("vacancies", r["data"]["id"])
    s, r = call("PUT", f"/vacancies/{vid}", vacancy_body(title="QA Physics Teacher", positions=3), token=ca, campus=a)
    check("update title and positions", s == 200 and r["data"]["title"] == "QA Physics Teacher" and r["data"]["positions"] == 3, (s, r))
    s, r = call("PUT", f"/vacancies/{vid}", vacancy_body(positions=0), token=ca, campus=a)
    check("positions_must_be_positive", s == 400, (s, r))
    s, r = call("POST", "/vacancies", vacancy_body(title="  "), token=ca, campus=a)
    check("title required", s == 400, (s, r))
    s, r = call("POST", "/vacancies", vacancy_body(), token=hq)
    check("create needs a campus", s == 400, (s, r))
    s, r = call("POST", "/vacancies", vacancy_body(classIds=[999999]), token=ca, campus=a)
    check("unknown class id refused", s == 400, (s, r))
    for st in ("OnHold", "Closed", "Open"):
        s, r = call("POST", f"/vacancies/{vid}/status", {"status": st}, token=ca, campus=a)
        check(f"set {st}", s == 200 and r["data"]["status"] == st, (s, r))
    s, r = call("POST", f"/vacancies/{vid}/status", {"status": "Filled"}, token=ca, campus=a)
    check("set Filled by hand -> 400", s == 400, (s, r))
    s, r = call("GET", "/vacancies?status=Open", token=ca, campus=a)
    check("list status=Open contains it", s == 200 and any(v["id"] == vid for v in r["data"]), (s, r))
    s, r = call("GET", "/vacancies?status=Closed", token=ca, campus=a)
    check("list status=Closed excludes it", s == 200 and not any(v["id"] == vid for v in r["data"]), s)
    s, r = call("GET", f"/vacancies/{vid}", token=ca, campus=a)
    check("get one", s == 200 and r["data"]["id"] == vid, (s, r))
    # candidate counts show up and a closed vacancy refuses candidates
    make_candidate(ca, a, vid)
    s, r = call("GET", f"/vacancies/{vid}", token=ca, campus=a)
    check("stage counts include the candidate", s == 200 and r["data"]["counts"]["applied"] == 1, (s, r))
    call("POST", f"/vacancies/{vid}/status", {"status": "Closed"}, token=ca, campus=a)
    s, r = call("POST", "/candidates", candidate_body(vid), token=ca, campus=a)
    check("candidate on a closed vacancy -> 400", s == 400 and r.get("message") == "This vacancy is not open.", (s, r))
    s, _ = call("DELETE", f"/vacancies/{vid}", token=ca, campus=a)
    check("vacancy with candidates cannot be deleted", s == 400, s)


# ------------------------------------------------------------------ pipeline

@group
def pipeline(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    vid, _ = make_vacancy(ca, a)
    cid, p = make_candidate(ca, a, vid)
    check("new candidate is Applied with history [Applied]", p["candidate"]["stage"] == "Applied"
          and [h["toStage"] for h in p["history"]] == ["Applied"], p)
    check("allowed_moves_listed", p["allowedMoves"] == ["Shortlisted", "Rejected", "Withdrawn"], p["allowedMoves"])
    s, r = move(ca, a, cid, "Interview")
    check("Applied -> Interview refused", s == 400 and "Allowed next stages" in (r.get("message") or ""), (s, r))
    s, r = move(ca, a, cid, "Shortlisted")
    check("Applied -> Shortlisted", s == 200 and r["data"]["candidate"]["stage"] == "Shortlisted", (s, r))
    s, r = move(ca, a, cid, "Rejected")
    check("reject without reason -> 400", s == 400 and r.get("message") == "A reason is required.", (s, r))
    s, r = move(ca, a, cid, "Rejected", "Not a fit")
    check("reject with reason", s == 200 and r["data"]["candidate"]["stage"] == "Rejected"
          and r["data"]["candidate"]["stageReason"] == "Not a fit", (s, r))
    s, r = move(ca, a, cid, "Applied")
    check("reopen needs a reason", s == 400, (s, r))
    s, r = move(ca, a, cid, "Applied", "Second look")
    check("reopen -> Applied", s == 200 and r["data"]["candidate"]["stage"] == "Applied"
          and len(r["data"]["history"]) == 4, (s, r))

    # reopen_needs_open_vacancy
    move(ca, a, cid, "Rejected", "No")
    call("POST", f"/vacancies/{vid}/status", {"status": "Closed"}, token=ca, campus=a)
    s, r = move(ca, a, cid, "Applied", "Again")
    check("reopen_needs_open_vacancy", s == 400 and r.get("message") == "This vacancy is not open.", (s, r))
    call("POST", f"/vacancies/{vid}/status", {"status": "Open"}, token=ca, campus=a)

    # cnic_reusable_after_delete
    cnic = rand_digits(13)
    c1, _ = make_candidate(ca, a, vid, cnic=cnic)
    s, r = call("POST", "/candidates", candidate_body(vid, cnic=cnic[:5] + "-" + cnic[5:12] + "-" + cnic[12:]), token=ca, campus=a)
    check("same CNIC (dashed) twice in a vacancy -> 409", s == 409, (s, r))
    s, _ = call("DELETE", f"/candidates/{c1}", token=ca, campus=a)
    check("delete an Applied candidate", s == 200, s)
    s, r = call("POST", "/candidates", candidate_body(vid, cnic=cnic), token=ca, campus=a)
    check("cnic_reusable_after_delete", s == 200, (s, r))
    if s == 200:
        c2 = track("candidates", r["data"]["candidate"]["id"])
        check("CNIC stored digits-only", r["data"]["candidate"]["cnic"] == cnic, r["data"]["candidate"].get("cnic"))
        move(ca, a, c2, "Shortlisted")
        s, _ = call("DELETE", f"/candidates/{c2}", token=ca, campus=a)
        check("cannot delete a Shortlisted candidate", s == 400, s)

    # list / search
    s, r = call("GET", f"/candidates?vacancyId={vid}&stage=active", token=ca, campus=a)
    check("active list excludes rejected", s == 200 and all(i["stage"] not in ("Rejected", "Withdrawn", "Hired") for i in r["data"]["items"])
          and not any(i["id"] == cid for i in r["data"]["items"]), (s, r))
    s, r = call("GET", f"/candidates?q={cnic[-6:]}", token=ca, campus=a)
    check("search by CNIC digits", s == 200 and r["data"]["total"] >= 1, (s, r))
    retire_candidates(ca)


# ------------------------------------------------------------------ cv

@group
def cv(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    vid, _ = make_vacancy(ca, a)
    cid, _ = make_candidate(ca, a, vid)
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "my cv.pdf", PDF, "application/pdf", ca, a)
    check("upload PDF CV", s == 200, (s, r))
    p = data(call("GET", f"/candidates/{cid}", token=ca, campus=a))
    check("hasCv true", p["candidate"]["hasCv"] and p["candidate"]["cvFileName"] == "my cv.pdf", p["candidate"])
    s, body, headers = download(f"/candidates/{cid}/cv", ca, a)
    disp = headers.get("Content-Disposition", "")
    check("download byte-identical with file name", s == 200 and body == PDF and "my cv.pdf" in _re.sub(r"%20", " ", disp), (s, disp))
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "cv.docx", DOCX, "application/octet-stream", ca, a)
    check("DOCX accepted", s == 200, (s, r))
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "cv.doc", DOC, "application/msword", ca, a)
    check("DOC accepted", s == 200, (s, r))
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "evil.pdf", b"MZ\x90\x00" + b"\x00" * 64, "application/pdf", ca, a)
    check("MZ renamed to .pdf -> 400", s == 400, (s, r))
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "cv.txt", b"plain text cv", "text/plain", ca, a)
    check(".txt -> 400", s == 400, (s, r))
    s, r = upload(f"/candidates/{cid}/cv", {}, "file", "big.pdf", PDF + b"\x00" * (10_485_761 - len(PDF)), "application/pdf", ca, a)
    check("10,485,761 bytes -> 400", s in (400, 413), (s, r if not isinstance(r, str) else r[:80]))


# ------------------------------------------------------------------ interviews

@group
def interviews(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    vid, _ = make_vacancy(ca, a)
    cid, _ = make_candidate(ca, a, vid)
    iv = interviewer(hq, a)
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "Demo", "scheduledAt": soon(), "interviewerUserId": iv}, token=ca, campus=a)
    check("schedule on Applied -> 400", s == 400, (s, r))
    move(ca, a, cid, "Shortlisted")
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "Demo lesson", "scheduledAt": soon(), "interviewerUserId": iv}, token=ca, campus=a)
    ok = check("schedule on Shortlisted -> stage Interview", s == 200 and r["data"]["candidate"]["stage"] == "Interview"
               and len(r["data"]["interviews"]) == 1 and r["data"]["interviews"][0]["result"] == "Scheduled", (s, r))
    if not ok:
        return
    r1 = r["data"]["interviews"][0]["id"]
    when = r["data"]["interviews"][0]["scheduledAt"]
    check("utc_times_marked: scheduledAt carries a UTC marker", when.endswith("Z") or when.endswith("+00:00"), when)
    hist = r["data"]["history"][-1]["occurredOn"]
    check("utc_times_marked: history time carries a UTC marker", hist.endswith("Z") or hist.endswith("+00:00"), hist)

    # pick list and inactive interviewer
    s, picks = call("GET", "/users?pick=true", token=ca, campus=a)
    check("pick list has the interviewer", s == 200 and any(u["id"] == iv for u in picks["data"]), (s, picks))
    off_id, off_email, off_pw = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    from sms_client import user_payload, role_id
    payload = user_payload(campus_ids=[a], role_ids=[role_id(hq, "Teacher")], email=off_email, password=off_pw)
    payload["isActive"] = False
    s, r = call("PUT", f"/users/{off_id}", payload, token=hq)
    check("deactivate a user", s == 200, (s, r))
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "HR", "scheduledAt": soon(), "interviewerUserId": off_id}, token=ca, campus=a)
    check("inactive_interviewer_refused", s == 400, (s, r))
    s, picks = call("GET", "/users?pick=true", token=ca, campus=a)
    check("inactive user absent from pick list", s == 200 and not any(u["id"] == off_id for u in picks["data"]), s)
    other = interviewer(hq, b)
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "HR", "scheduledAt": soon(), "interviewerUserId": other}, token=ca, campus=a)
    check("interviewer of another campus only -> 400", s == 400, (s, r))

    upd = {"name": "Demo lesson", "scheduledAt": soon(), "interviewerUserId": iv, "result": "Pass", "rating": None, "notes": "ok"}
    s, r = call("PUT", f"/candidates/{cid}/interviews/{r1}", upd, token=ca, campus=a)
    check("Pass without rating -> 400", s == 400, (s, r))
    s, r = call("PUT", f"/candidates/{cid}/interviews/{r1}", dict(upd, rating=6), token=ca, campus=a)
    check("rating 6 -> 400", s == 400, (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer", {"designation": "Teacher", "employmentType": "Permanent", "offeredSalary": 60000,
                                                      "proposedJoiningDate": _dt.date.today().isoformat()}, token=ca, campus=a)
    check("offer before a Pass -> 400", s == 400 and r.get("message") == "Record a passed interview first.", (s, r))
    s, r = call("PUT", f"/candidates/{cid}/interviews/{r1}", dict(upd, rating=4), token=ca, campus=a)
    check("Pass with rating 4", s == 200 and r["data"]["interviews"][0]["result"] == "Pass" and r["data"]["interviews"][0]["rating"] == 4, (s, r))
    s, _ = call("DELETE", f"/candidates/{cid}/interviews/{r1}", token=ca, campus=a)
    check("cannot_delete_decided_round", s == 400, s)
    s, r = call("POST", f"/candidates/{cid}/interviews", {"name": "HR", "scheduledAt": soon(2), "interviewerUserId": iv}, token=ca, campus=a)
    r2 = r["data"]["interviews"][-1]["id"] if s == 200 else None
    s, r = call("DELETE", f"/candidates/{cid}/interviews/{r2}", token=ca, campus=a)
    check("delete a Scheduled round", s == 200 and len(r["data"]["interviews"]) == 1, (s, r))

    offer = {"designation": "Teacher", "employmentType": "Permanent", "offeredSalary": 0, "proposedJoiningDate": _dt.date.today().isoformat()}
    s, r = call("POST", f"/candidates/{cid}/offer", offer, token=ca, campus=a)
    check("salary 0 -> 400", s == 400, (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer", dict(offer, offeredSalary=60000), token=ca, campus=a)
    check("offer after a Pass -> Offered, Pending", s == 200 and r["data"]["candidate"]["stage"] == "Offered"
          and r["data"]["offer"]["status"] == "Pending", (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer/decline", token=ca, campus=a)
    check("decline keeps stage Offered", s == 200 and r["data"]["offer"]["status"] == "Declined"
          and r["data"]["candidate"]["stage"] == "Offered", (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer", dict(offer, offeredSalary=65000), token=ca, campus=a)
    check("new offer after decline", s == 200 and r["data"]["offer"]["status"] == "Pending" and r["data"]["offer"]["offeredSalary"] == 65000, (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer/accept", token=ca, campus=a)
    check("accept", s == 200 and r["data"]["offer"]["status"] == "Accepted" and r["data"]["offer"].get("respondedOn"), (s, r))
    s, r = call("POST", f"/candidates/{cid}/offer/accept", token=ca, campus=a)
    check("accept again -> 400", s == 400, (s, r))
    retire_candidates(ca)


# ------------------------------------------------------------------ hire

@group
def hire(hq):
    a, _ = demo_campuses(hq)
    code = campus_code(hq, a)
    _, ca = campus_admin(hq, a)
    iv = interviewer(hq, a)
    vid, _ = make_vacancy(ca, a, positions=1)
    name = "Qa Hire " + rand_digits(5)
    cnic = rand_digits(13)
    cid, _ = make_candidate(ca, a, vid, fullName=name, cnic=cnic)
    move(ca, a, cid, "Shortlisted")
    s, r = call("POST", f"/candidates/{cid}/hire", hire_body(), token=ca, campus=a)
    check("hire without an accepted offer -> 400", s == 400, (s, r))
    to_offer_accepted(ca, a, cid, iv)
    s, r = call("POST", f"/candidates/{cid}/hire", hire_body(61000), token=ca, campus=a)
    ok = check("hire -> staff", s == 200 and _re.fullmatch(rf"{_re.escape(code)}-EMP-\d{{4}}", r["data"]["employeeNo"] or "")
               and r["data"]["status"] == "Onboarding" and r["data"]["basicSalary"] == 61000
               and r["data"]["lastName"] == name.split()[-1], (s, r))
    if not ok:
        return
    p = data(call("GET", f"/candidates/{cid}", token=ca, campus=a))
    check("candidate is Hired with staff id", p["candidate"]["stage"] == "Hired" and p["candidate"]["staffId"] == r["data"]["id"]
          and p["allowedMoves"] == [], p["candidate"])
    v = data(call("GET", f"/vacancies/{vid}", token=ca, campus=a))
    check("vacancy_filled_at_positions", v["status"] == "Filled" and v["hired"] == 1 and v["counts"]["hired"] == 1, v)
    s, r2 = call("POST", f"/vacancies/{vid}/status", {"status": "Open"}, token=ca, campus=a)
    check("reopen a Filled vacancy without more positions -> 400", s == 400, (s, r2))
    s, r2 = call("PUT", f"/vacancies/{vid}", vacancy_body(positions=0), token=ca, campus=a)
    check("positions below hired -> 400", s == 400, (s, r2))
    s, r2 = call("POST", f"/candidates/{cid}/hire", hire_body(), token=ca, campus=a)
    check("hire twice -> 409", s == 409 and r2.get("message") == "This candidate is already hired.", (s, r2))
    s, r2 = call("GET", f"/staff?q={_quote(name)}", token=ca, campus=a)
    check("staff search finds the new staff", s == 200 and any(x["id"] == r["data"]["id"] for x in r2["data"]["items"]), (s, r2))
    s, r2 = call("GET", f"/staff/{r['data']['id']}", token=ca, campus=a)
    check("staff detail", s == 200 and r2["data"]["staff"]["sourceCandidateId"] == cid, (s, r2))

    # active_staff_cnic_refused + existing_staff_warning
    vid2, _ = make_vacancy(ca, a, positions=3)
    c2, p2 = make_candidate(ca, a, vid2, cnic=cnic)
    check("existing_staff_warning", (p2["candidate"].get("existingStaffWarning") or "") == f"Already on staff as {r['data']['employeeNo']}",
          p2["candidate"].get("existingStaffWarning"))
    to_offer_accepted(ca, a, c2, iv)
    s, r2 = call("POST", f"/candidates/{c2}/hire", hire_body(), token=ca, campus=a)
    check("active_staff_cnic_refused", s == 409 and r2.get("message") == "This CNIC belongs to an active staff member.", (s, r2))

    # concurrent_hire_creates_one_staff
    c3, _ = make_candidate(ca, a, vid2)
    to_offer_accepted(ca, a, c3, iv)
    codes = []

    def go():
        codes.append(call("POST", f"/candidates/{c3}/hire", hire_body(), token=ca, campus=a)[0])

    threads = [_threading.Thread(target=go) for _ in range(2)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    check("concurrent_hire_creates_one_staff: codes", sorted(codes) == [200, 409], codes)
    staff = data(call("GET", "/staff?pageSize=100", token=ca, campus=a))["items"]
    p3 = data(call("GET", f"/candidates/{c3}", token=ca, campus=a))
    mine = [x for x in staff if x["id"] == p3["candidate"].get("staffId")]
    check("concurrent_hire_creates_one_staff: one row", len(mine) == 1 and p3["candidate"]["stage"] == "Hired", (len(mine), p3["candidate"]))
    retire_candidates(ca)


# ------------------------------------------------------------------ isolation, permissions, dashboard

@group
def hiring_isolation(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    iv_b = interviewer(hq, b)
    vb, vdto = make_vacancy(hq, b, positions=5)
    cb, _ = make_candidate(hq, b, vb)
    upload(f"/candidates/{cb}/cv", {}, "file", "b.pdf", PDF, "application/pdf", hq, b)
    ch, _ = make_candidate(hq, b, vb, fullName="Qa Iso " + rand_digits(5))
    to_offer_accepted(hq, b, ch, iv_b)
    s, staff = call("POST", f"/candidates/{ch}/hire", hire_body(), token=hq, campus=b)
    sid = staff["data"]["id"] if s == 200 else 0
    move(hq, b, cb, "Shortlisted")
    s, r = call("POST", f"/candidates/{cb}/interviews", {"name": "B", "scheduledAt": soon(), "interviewerUserId": iv_b}, token=hq, campus=b)
    rid = r["data"]["interviews"][0]["id"]
    iv_a = interviewer(hq, a)
    attempts = [
        ("GET vacancy", call("GET", f"/vacancies/{vb}", token=ca, campus=a)[0]),
        ("PUT vacancy", call("PUT", f"/vacancies/{vb}", vacancy_body(title="hacked"), token=ca, campus=a)[0]),
        ("vacancy status", call("POST", f"/vacancies/{vb}/status", {"status": "Closed"}, token=ca, campus=a)[0]),
        ("DELETE vacancy", call("DELETE", f"/vacancies/{vb}", token=ca, campus=a)[0]),
        ("GET candidate", call("GET", f"/candidates/{cb}", token=ca, campus=a)[0]),
        ("PUT candidate", call("PUT", f"/candidates/{cb}", candidate_body(vb, fullName="hacked"), token=ca, campus=a)[0]),
        ("move", move(ca, a, cb, "Rejected", "x")[0]),
        ("schedule", call("POST", f"/candidates/{cb}/interviews", {"name": "x", "scheduledAt": soon(), "interviewerUserId": iv_a}, token=ca, campus=a)[0]),
        ("update round", call("PUT", f"/candidates/{cb}/interviews/{rid}", {"name": "x", "scheduledAt": soon(), "interviewerUserId": iv_a,
                                                                          "result": "Fail", "rating": 1, "notes": None}, token=ca, campus=a)[0]),
        ("delete round", call("DELETE", f"/candidates/{cb}/interviews/{rid}", token=ca, campus=a)[0]),
        ("offer", call("POST", f"/candidates/{cb}/offer", {"designation": "Teacher", "employmentType": "Permanent", "offeredSalary": 1,
                                                          "proposedJoiningDate": _dt.date.today().isoformat()}, token=ca, campus=a)[0]),
        ("offer accept", call("POST", f"/candidates/{ch}/offer/accept", token=ca, campus=a)[0]),
        ("hire", call("POST", f"/candidates/{cb}/hire", hire_body(), token=ca, campus=a)[0]),
        ("cv", download(f"/candidates/{cb}/cv", ca, a)[0]),
        ("cv upload", upload(f"/candidates/{cb}/cv", {}, "file", "x.pdf", PDF, "application/pdf", ca, a)[0]),
        ("GET staff", call("GET", f"/staff/{sid}", token=ca, campus=a)[0]),
        ("DELETE candidate", call("DELETE", f"/candidates/{cb}", token=ca, campus=a)[0]),
    ]
    check("every cross-campus hiring action -> 404", all(code == 404 for _, code in attempts), attempts)
    s, r = call("POST", "/candidates", candidate_body(vb), token=ca, campus=a)
    check("add candidate to another campus's vacancy -> 400", s == 400, (s, r))
    lists = [
        ("vacancies", [v["id"] for v in data(call("GET", "/vacancies", token=ca, campus=a))]),
        ("candidates", [c["id"] for c in data(call("GET", f"/candidates?q={_quote('Qa')}&pageSize=100", token=ca, campus=a))["items"]]),
        ("staff", [x["id"] for x in data(call("GET", "/staff?pageSize=100", token=ca, campus=a))["items"]]),
    ]
    check("lists hide campus-B rows", vb not in lists[0][1] and cb not in lists[1][1] and ch not in lists[1][1] and sid not in lists[2][1], lists)
    after = data(call("GET", f"/candidates/{cb}", token=hq, campus=b))
    v_after = data(call("GET", f"/vacancies/{vb}", token=hq, campus=b))
    check("campus-B rows unchanged", after["candidate"]["stage"] == "Interview" and after["candidate"]["fullName"] != "hacked"
          and len(after["interviews"]) == 1 and after["interviews"][0]["result"] == "Scheduled"
          and v_after["title"] == vdto["title"] and v_after["status"] == "Open", (after["candidate"], v_after))
    s, picks = call("GET", "/users?pick=true", token=ca, campus=a)
    check("campus-A pick list hides campus-B-only users", s == 200 and not any(u["id"] == iv_b for u in picks["data"]), s)
    retire_candidates(hq)


@group
def hiring_permissions(hq):
    a, _ = demo_campuses(hq)
    vid, _ = make_vacancy(hq, a)
    cid, _ = make_candidate(hq, a, vid)
    for role in ("Teacher", "Clerk", "Accountant"):
        _, e, p = make_user(hq, campus_ids=[a], role_names=[role])
        tok = login(e, p)["tokens"]["accessToken"]
        codes = [call("GET", path, token=tok, campus=a)[0] for path in
                 ("/vacancies", f"/vacancies/{vid}", "/candidates", f"/candidates/{cid}", "/staff")]
        codes.append(call("POST", "/vacancies", vacancy_body(), token=tok, campus=a)[0])
        codes.append(download(f"/candidates/{cid}/cv", tok, a)[0])
        check(f"{role}: every hiring endpoint -> 403", all(c == 403 for c in codes), codes)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Principal"])
    tok = login(e, p)["tokens"]["accessToken"]
    check("Principal: GET /vacancies -> 200", call("GET", "/vacancies", token=tok, campus=a)[0] == 200)
    check("Principal: users pick list -> 200", call("GET", "/users?pick=true", token=tok, campus=a)[0] == 200)
    check("Principal: classes and subjects pickers -> 200", call("GET", "/classes", token=tok, campus=a)[0] == 200
          and call("GET", "/subjects", token=tok, campus=a)[0] == 200)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    s, _ = call("GET", "/users", token=login(e, p)["tokens"]["accessToken"], campus=a)
    check("plain users list still needs users:View", s == 403, s)


@group
def dashboard_hiring(hq):
    a, _ = demo_campuses(hq)
    vid, _ = make_vacancy(hq, a)
    kpis = data(call("GET", "/dashboard", token=hq, campus=a))["kpis"]
    k = next((x for x in kpis if x["id"] == "hiring"), None)
    open_count = len(data(call("GET", "/vacancies?status=Open", token=hq, campus=a)))
    check("hiring KPI = open vacancies", k and k["value"] == str(open_count) and k.get("href") == "/admin/hiring/candidates", k)
    check("hint is 'N interviews this week'", k and _re.fullmatch(r"\d+ interviews? this week", k.get("hint") or ""), k)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    tok = login(e, p)["tokens"]["accessToken"]
    kpis = data(call("GET", "/dashboard", token=tok, campus=a))["kpis"]
    check("no hiring KPI for a Teacher", not any(x["id"] == "hiring" for x in kpis), [x["id"] for x in kpis])


# ------------------------------------------------------------------ review fixes

@group
def hiring_review_fixes(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    vid, _ = make_vacancy(ca, a)
    # long_name_refused: names longer than a staff name part (100) are refused up front, not at hire
    s, r = call("POST", "/candidates", candidate_body(vid, fullName="X" * 120), token=ca, campus=a)
    check("long_name_refused: 120-char name -> 400", s == 400, (s, r))
    # rounds_survive_deleted_interviewer
    cid, _ = make_candidate(ca, a, vid)
    move(ca, a, cid, "Shortlisted")
    iv = interviewer(hq, a)
    call("POST", f"/candidates/{cid}/interviews", {"name": "Screening", "scheduledAt": soon(), "interviewerUserId": iv}, token=ca, campus=a)
    s, _ = call("DELETE", f"/users/{iv}", token=hq)
    p = data(call("GET", f"/candidates/{cid}", token=ca, campus=a))
    check("rounds_survive_deleted_interviewer", s == 200 and len(p["interviews"]) == 1 and p["interviews"][0]["interviewerName"], (s, p["interviews"]))
    # picker_for_candidate_campus: Main Campus user on All campuses picks for the candidate's campus
    iv2 = interviewer(hq, a)
    s, r = call("GET", f"/users?pick=true&candidateId={cid}", token=hq)
    check("picker_for_candidate_campus", s == 200 and any(u["id"] == iv2 for u in r["data"]), (s, r))
    _, cb_tok = campus_admin(hq, demo_campuses(hq)[1])
    s, _ = call("GET", f"/users?pick=true&candidateId={cid}", token=cb_tok, campus=demo_campuses(hq)[1])
    check("picker_for_candidate_campus: other campus candidate -> 404", s == 404, s)
    retire_candidates(ca)


# ------------------------------------------------------------------ list-filters (source, applied range, per-stage paging)

@group
def list_filters(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    _, cb = campus_admin(hq, b)
    tag = "Lf" + rand_digits(5)
    vid, _ = make_vacancy(ca, a, positions=5)
    ids = {}
    for letter, source in (("A", "WalkIn"), ("B", "Referral"), ("C", "Referral"), ("D", "JobPortal"), ("E", "WalkIn")):
        ids[letter], _ = make_candidate(ca, a, vid, fullName=f"Qa {tag} {letter}", source=source)
    move(ca, a, ids["E"], "Shortlisted")
    by_id = {v: k for k, v in ids.items()}
    utc = _dt.datetime.now(_dt.timezone.utc).date()
    day = lambda n: (utc + _dt.timedelta(days=n)).isoformat()

    def get(token=ca, campus=a, **params):
        qs = "&".join(f"{k}={_quote(str(v))}" for k, v in dict(q=tag, **params).items())
        s, r = call("GET", f"/candidates?{qs}", token=token, campus=campus)
        return s, (r["data"] if s == 200 else r)

    def letters(page):
        return [by_id.get(x["id"], "?") for x in page["items"]]

    s, p = get()
    check("candidates default: newest first, unchanged", s == 200 and p["total"] == 5 and letters(p) == ["E", "D", "C", "B", "A"], (s, p))
    s, p = get(source="Referral")
    check("source filter narrows", s == 200 and p["total"] == 2 and letters(p) == ["C", "B"], (s, p))
    s, p = get(source="walkin", stage="active")
    check("source filter case-insensitive, combines with stage", s == 200 and letters(p) == ["E", "A"], (s, p))
    s, p = get(source="Billboard")
    check("unknown source -> 400", s == 400, (s, p))
    s, p = get(appliedFrom=day(0), appliedTo=day(0))
    check("applied range inclusive (today..today)", s == 200 and p["total"] == 5, (s, p))
    s, p = get(appliedTo=day(-1))
    check("appliedTo yesterday excludes today's", s == 200 and p["total"] == 0, (s, p))
    s, p = get(appliedFrom=day(1))
    check("appliedFrom tomorrow excludes today's", s == 200 and p["total"] == 0, (s, p))

    seen = []
    for page in (1, 2, 3):
        s, p = get(vacancyId=vid, stage="Applied", pageSize=2, page=page)
        check(f"per-stage paging page {page}: total is the stage total", s == 200 and p["total"] == 4
              and len(p["items"]) == (2 if page < 3 else 0), (s, p))
        seen += letters(p) if s == 200 else []
    check("per-stage paging covers the column once", seen == ["D", "C", "B", "A"], seen)
    s, p = get(vacancyId=vid, stage="Shortlisted", pageSize=1)
    check("other column has its own total", s == 200 and p["total"] == 1 and letters(p) == ["E"], (s, p))

    s, p = get(token=cb, campus=b, source="Referral")
    check("campus-B admin cannot see campus-A rows via source", s == 200 and p["total"] == 0, (s, p))
    s, p = get(token=cb, campus=b, appliedFrom=day(0), appliedTo=day(0), stage="Applied")
    check("campus-B admin cannot see campus-A rows via applied range", s == 200 and p["total"] == 0, (s, p))
    retire_candidates(ca)


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
