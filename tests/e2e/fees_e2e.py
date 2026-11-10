"""End-to-end checks for Fees.

Usage: python tests/e2e/fees_e2e.py <group> [<group> ...]
Runs against the local dev API. Groups set the fee structure they need and clear it again; challans and payments
they create are cancelled / voided where the API allows. Names of test data start with "QA".
"""
import datetime as _dt
import random as _random
import re as _re
import sys
import threading

from sms_client import (admin_token, call, check, cleanup, data, download, login, make_user, summary, track, wait_for_api)
from academics_e2e import actions, campus_admin, demo_campuses, menu_keys, role_detail

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def rnd(n):
    return "".join(_random.choice("0123456789") for _ in range(n))


def iso(d):
    return d.isoformat()


def today():
    return _dt.date.today()


def month_start(d):
    return d.replace(day=1)


def add_months(d, n):
    y, m = divmod(d.month - 1 + n, 12)
    return _dt.date(d.year + y, m + 1, 1)


# ------------------------------------------------------------------ shared helpers

def current_session(token, campus):
    lst = data(call("GET", "/academic-sessions", token=token, campus=campus))
    return next(x for x in lst["items"] if x["id"] == lst["effectiveCurrentId"])


def class_id(token, campus, code="G1"):
    rows = data(call("GET", "/classes", token=token, campus=campus))
    return next(x["id"] for x in rows if x["code"] == code)


def heads(token, campus=None):
    return data(call("GET", "/fees/heads", token=token, campus=campus))


def head_id(token, name, campus=None):
    return next(h["id"] for h in heads(token, campus) if h["name"] == name)


def set_amount(token, session_id, cls, head, amount, campus=None, main=True):
    return call("PUT", "/fees/structure", {"sessionId": session_id, "mainCampus": main,
                                           "rows": [{"classId": cls, "feeHeadId": head, "amount": amount}]},
                token=token, campus=campus)


def cell(grid, cls, head):
    row = next(r for r in grid["rows"] if r["classId"] == cls)
    c = next(c for c in row["cells"] if c["feeHeadId"] == head)
    return {k: c.get(k) for k in ("feeHeadId", "mainAmount", "campusAmount", "effective")}


SETTINGS_KEYS = ("dueDay", "lateFine", "siblingSecondPercent", "siblingThirdPlusPercent", "staffChildPercent",
                 "concessionHeadIds", "bankName", "accountTitle", "accountNo", "challanNote")


def settings_body(over=None, main=False, blank=True):
    body = {k: None for k in SETTINGS_KEYS} if blank else {}
    body.update(over or {})
    body["mainCampus"] = main
    return body


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    me = data(call("GET", "/auth/me", token=hq))
    check("menu has Fees after Exams", menu_keys(me) == ["dashboard", "campuses", "academics", "students-menu", "staff-menu",
                                                         "attendance-menu", "exams-menu", "fees-menu", "settings"], menu_keys(me))
    grp = next((m for m in me["menu"] if m["key"] == "fees-menu"), None)
    keys = ["fee-setup", "fee-vouchers", "fee-collection", "fee-reports"]
    check("fees children", grp and [c["key"] for c in grp["children"]] == keys, grp)
    full, view, none = {"view", "add", "edit", "delete"}, {"view"}, set()
    expected = {
        "Principal": {k: full for k in keys},
        "CampusAdmin": {k: full for k in keys},
        "Accountant": {k: full for k in keys},
        "Clerk": {"fee-setup": none, "fee-vouchers": view, "fee-collection": {"view", "add"}, "fee-reports": none},
        "Teacher": {k: none for k in keys},
    }
    for role, want in expected.items():
        got = {k: actions(role_detail(hq, role), k) for k in keys}
        check(f"{role} fee grants", got == want, got)
    hs = [h for h in heads(hq) if h.get("campusId") is None]
    got = [(h["name"], h["frequency"], h.get("oneTimeMonth"), h["isActive"]) for h in sorted(hs, key=lambda h: h["sortOrder"])][:4]
    check("seeded Main Campus heads", got == [("Tuition", "Monthly", None, True), ("Admission", "OneTime", None, True),
                                              ("Annual Fund", "OneTime", 4, True), ("Exam", "OneTime", None, False)], got)
    st = data(call("GET", "/fees/settings", token=hq))
    tuition = head_id(hq, "Tuition")
    check("seeded settings", (st["dueDay"], st["lateFine"], st["concessionHeadIds"]) == (10, 300, [tuition]), st)


# ------------------------------------------------------------------ setup

@group
def setup(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sess = current_session(hq, a)["id"]
    g1 = class_id(hq, a)
    tuition = head_id(hq, "Tuition")
    try:
        s, r = set_amount(hq, sess, g1, tuition, 3000)
        check("Main Campus sets G1 Tuition 3000", s == 200, (s, r))
        c = cell(data(call("GET", f"/fees/structure?sessionId={sess}", token=ca, campus=a)), g1, tuition)
        check("campus sees the Main Campus amount", (c["mainAmount"], c["campusAmount"], c["effective"]) == (3000, None, 3000), c)
        s, r = set_amount(ca, sess, g1, tuition, 3500, campus=a, main=False)
        check("campus overrides to 3500", s == 200, (s, r))
        c = cell(data(call("GET", f"/fees/structure?sessionId={sess}", token=ca, campus=a)), g1, tuition)
        check("override wins for the campus", (c["mainAmount"], c["campusAmount"], c["effective"]) == (3000, 3500, 3500), c)
        cb = cell(data(call("GET", f"/fees/structure?sessionId={sess}", token=hq, campus=b)), g1, tuition)
        check("other campus still 3000", cb["effective"] == 3000, cb)
        set_amount(ca, sess, g1, tuition, None, campus=a, main=False)
        c = cell(data(call("GET", f"/fees/structure?sessionId={sess}", token=ca, campus=a)), g1, tuition)
        check("removing the override falls back", (c["campusAmount"], c["effective"]) == (None, 3000), c)
        s, r = set_amount(ca, sess, g1, tuition, 100, campus=a, main=True)
        check("campus cannot change Main Campus amounts", s == 403, (s, r))
        s, r = set_amount(hq, sess, g1, tuition, -1)
        check("negative amount -> 400", s == 400, (s, r))

        # heads
        s, r = call("PUT", f"/fees/heads/{tuition}", {"name": "Tuition", "frequency": "Monthly", "oneTimeMonth": None, "sortOrder": 1,
                                                      "isActive": True, "mainCampus": True}, token=ca, campus=a)
        check("campus cannot edit a Main Campus head", s == 403, (s, r))
        name = "QA Lab " + rnd(4)
        s, r = call("POST", "/fees/heads", {"name": name, "frequency": "Monthly", "oneTimeMonth": None, "sortOrder": 9,
                                            "isActive": True, "mainCampus": False}, token=ca, campus=a)
        check("campus creates its own head", s == 200 and r["data"].get("campusId") == a, (s, r))
        mine = r["data"]["id"] if s == 200 else None
        check("own head visible to the campus only", any(h["name"] == name for h in heads(ca, a))
              and not any(h["name"] == name for h in heads(hq, b)), name)
        s, r = call("POST", "/fees/heads", {"name": name, "frequency": "Monthly", "oneTimeMonth": None, "sortOrder": 9,
                                            "isActive": True, "mainCampus": False}, token=ca, campus=a)
        check("duplicate head name -> 409", s == 409, (s, r))
        s, r = call("POST", "/fees/heads", {"name": "QA Bad", "frequency": "OneTime", "oneTimeMonth": 13, "sortOrder": 9,
                                            "isActive": True, "mainCampus": False}, token=ca, campus=a)
        check("month 13 -> 400", s == 400, (s, r))
        if mine:
            s, _ = call("DELETE", f"/fees/heads/{mine}", token=ca, campus=a)
            check("delete an unused campus head", s == 200, s)

        # settings fallback
        s, r = call("PUT", "/fees/settings", settings_body({"lateFine": 500}), token=ca, campus=a)
        check("campus saves a late fine only", s == 200, (s, r))
        st = data(call("GET", "/fees/settings", token=ca, campus=a))
        check("campus fine 500, due day falls back to 10", (st["lateFine"], st["dueDay"], st["hasCampusOverride"]) == (500, 10, True), st)
        st_b = data(call("GET", "/fees/settings", token=hq, campus=b))
        check("other campus keeps 300", st_b["lateFine"] == 300, st_b)
        s, r = call("PUT", "/fees/settings", settings_body({"dueDay": 31}), token=ca, campus=a)
        check("due day 31 -> 400", s == 400, (s, r))
    finally:
        call("DELETE", "/fees/settings", token=ca, campus=a)
        set_amount(ca, sess, g1, tuition, None, campus=a, main=False)
        set_amount(hq, sess, g1, tuition, None)


# ------------------------------------------------------------------ concessions

def link(gid):
    return [{"guardianId": gid, "newGuardian": None, "relation": "Father", "isPrimary": True, "isEmergencyContact": True}]


def admit_student(token, campus, section_id, guardians=None, **over):
    from students_e2e import admit
    s, r = admit(token, campus, section_id, guardians=guardians, **over)
    if s != 200:
        raise AssertionError(f"admit: {s} {r}")
    return r["data"]


def guardian_of(token, campus, student_id):
    d = data(call("GET", f"/students/{student_id}", token=token, campus=campus))
    return d["guardians"][0]


def preview(token, campus, student_id, month=None):
    m = iso(month or month_start(today()))
    return data(call("GET", f"/fees/concessions/preview?studentId={student_id}&month={m}", token=token, campus=campus))


def line(lines, name):
    x = next((l for l in lines if l["description"] == name), None)
    return None if x is None else (x["amount"], x["concession"], x.get("concessionNote"), x["net"])


def concession(token, campus, student, sess, head, kind="Manual", value_type="Percent", value=10, reason="QA test", staff=None):
    return call("POST", "/fees/concessions", {"studentId": student, "sessionId": sess, "feeHeadId": head, "kind": kind,
                                              "staffId": staff, "valueType": value_type, "value": value, "reason": reason},
                token=token, campus=campus)


class FeeWorld:
    """Main Campus G1 Tuition 3000 / Admission 5000 for the current session; restored on close()."""

    def __init__(self, hq, campus, tuition=3000, admission=5000):
        self.hq, self.campus = hq, campus
        self.sess = current_session(hq, campus)["id"]
        self.g1 = class_id(hq, campus)
        self.tuition, self.admission = head_id(hq, "Tuition"), head_id(hq, "Admission")
        set_amount(hq, self.sess, self.g1, self.tuition, tuition)
        set_amount(hq, self.sess, self.g1, self.admission, admission)

    def close(self):
        set_amount(self.hq, self.sess, self.g1, self.tuition, None)
        set_amount(self.hq, self.sess, self.g1, self.admission, None)


@group
def concessions(hq):
    from attendance_e2e import qa_section
    from onboarding_e2e import make_staff, retire_staff
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    made = []
    try:
        call("PUT", "/fees/settings", settings_body({"siblingSecondPercent": 10, "siblingThirdPlusPercent": 20,
                                                     "staffChildPercent": 50}), token=ca, campus=a)
        sec, _ = qa_section(hq, a)
        sec_b, _ = qa_section(hq, b)
        s1 = admit_student(ca, a, sec)["student"]["id"]
        g = guardian_of(ca, a, s1)
        admit_student(hq, b, sec_b, guardians=link(g["guardianId"]))
        s2 = admit_student(ca, a, sec, guardians=link(g["guardianId"]))["student"]["id"]
        s3 = admit_student(ca, a, sec, guardians=link(g["guardianId"]))["student"]["id"]

        check("first child pays full (B sibling not counted)", line(preview(ca, a, s1), "Tuition") == (3000, 0, None, 3000), preview(ca, a, s1))
        check("second child sibling 10%", line(preview(ca, a, s2), "Tuition") == (3000, 300, "Sibling 10%", 2700), preview(ca, a, s2))
        check("third child sibling 20%", line(preview(ca, a, s3), "Tuition") == (3000, 600, "Sibling 20%", 2400), preview(ca, a, s3))

        s, r = concession(ca, a, s1, w.sess, w.tuition, value=20, reason="QA merit")
        check("manual 20% saved", s == 200, (s, r))
        c1 = r["data"]["id"] if s == 200 else None
        made.append(c1)
        check("manual 20% -> 600", line(preview(ca, a, s1), "Tuition") == (3000, 600, "Manual: QA merit", 2400), preview(ca, a, s1))
        s, r = call("PUT", f"/fees/concessions/{c1}", {"studentId": s1, "sessionId": w.sess, "feeHeadId": w.tuition, "kind": "Manual",
                                                       "staffId": None, "valueType": "Fixed", "value": 5000, "reason": "QA merit"},
                    token=ca, campus=a)
        check("fixed 5000 is capped at the amount", s == 200 and line(preview(ca, a, s1), "Tuition")[1] == 3000, (s, r))
        call("DELETE", f"/fees/concessions/{c1}", token=ca, campus=a)

        s, r = concession(ca, a, s2, w.sess, w.tuition, value=5, reason="QA small")
        made.append(r["data"]["id"] if s == 200 else None)
        check("largest wins: sibling 10% over manual 5%", line(preview(ca, a, s2), "Tuition") == (3000, 300, "Sibling 10%", 2700), preview(ca, a, s2))

        s, r = concession(ca, a, s1, w.sess, None, value=50, reason="QA all monthly")
        check("all-monthly concession saved", s == 200, (s, r))
        c_all = r["data"]["id"] if s == 200 else None
        lines = preview(ca, a, s1)
        check("all-monthly 50% on Tuition, not on Admission", line(lines, "Tuition")[1] == 1500 and line(lines, "Admission")[1] == 0, lines)
        call("DELETE", f"/fees/concessions/{c_all}", token=ca, campus=a)

        s, r = concession(ca, a, s1, w.sess, w.tuition, value=120)
        check("percent above 100 -> 400", s == 400, (s, r))
        s, r = concession(ca, a, s2, w.sess, w.tuition, value=7)
        check("duplicate active concession -> 409", s == 409, (s, r))

        staff_id, _ = make_staff(hq, a, cnic=g.get("cnic") or "")
        sug = data(call("GET", "/fees/concessions/staff-child-suggestions", token=ca, campus=a))
        mine = [x for x in sug if x["staffId"] == staff_id]
        check("staff-child suggestions for the three children", sorted(x["studentId"] for x in mine) == sorted([s1, s2, s3]), sug)
        check("CNIC masked", all(_re.fullmatch(r"\*{5}-\*{7}-\d", x["maskedCnic"]) for x in mine), mine[:1])
        s, r = concession(ca, a, s1, w.sess, None, kind="StaffChild", value=None, staff=staff_id, reason="QA staff child")
        check("staff-child confirmed with the settings %", s == 200 and r["data"]["value"] == 50, (s, r))
        made.append(r["data"]["id"] if s == 200 else None)
        check("staff child 50% -> 1500", line(preview(ca, a, s1), "Tuition") == (3000, 1500, "Staff child 50%", 1500), preview(ca, a, s1))
        sug = data(call("GET", "/fees/concessions/staff-child-suggestions", token=ca, campus=a))
        check("confirmed child leaves the suggestions", not any(x["studentId"] == s1 for x in sug), sug)
        lst = data(call("GET", f"/fees/concessions?sessionId={w.sess}", token=ca, campus=a))
        check("list shows the concessions", lst["total"] >= 2 and any(x["studentId"] == s1 for x in lst["items"]), lst)
    finally:
        for cid in made:
            if cid:
                call("DELETE", f"/fees/concessions/{cid}", token=ca, campus=a)
        call("DELETE", "/fees/settings", token=ca, campus=a)
        w.close()
        retire_staff(hq)


# ------------------------------------------------------------------ generate

def gen(token, campus, month, preview=False, section=None, cls=None, issue=None, due=None):
    body = {"month": iso(month), "classId": cls, "sectionId": section, "issueDate": iso(issue or today()),
            "dueDate": iso(due) if due else None, "preview": preview}
    return call("POST", "/fees/vouchers/generate", body, token=token, campus=campus)


def vouchers(token, campus, **filters):
    qs = "&".join(f"{k}={v}" for k, v in filters.items() if v is not None)
    return data(call("GET", f"/fees/vouchers?pageSize=100&{qs}", token=token, campus=campus))


def voucher(token, campus, vid):
    return data(call("GET", f"/fees/vouchers/{vid}", token=token, campus=campus))


def lines_of(detail):
    return {l["description"]: l["net"] for l in detail["lines"]}


def campus_code_of(token, campus_id):
    return data(call("GET", f"/campuses/{campus_id}", token=token))["code"]


def other_class_section(token, campus, not_class):
    """A fresh QA section in some class other than `not_class`."""
    rows = data(call("GET", "/sections", token=token, campus=campus))
    code = next(x["classCode"] for x in rows if x["classId"] != not_class)
    from attendance_e2e import qa_section
    return qa_section(token, campus, class_code=code)


@group
def generate(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    annual = head_id(hq, "Annual Fund")
    sess = current_session(hq, a)
    try:
        code = campus_code_of(hq, a)
        sec, _ = qa_section(hq, a)
        studs = [admit_student(ca, a, sec)["student"]["id"] for _ in range(3)]
        m0, m1 = month_start(today()), add_months(month_start(today()), 1)
        due0 = today() - _dt.timedelta(days=1)
        issue0 = due0 - _dt.timedelta(days=5)

        s, r = gen(ca, a, m0, preview=True, section=sec)
        p = r.get("data") or {}
        check("preview lists the section", s == 200 and p.get("toCreate") == 3 and len(p.get("rows", [])) == 3, (s, r))
        check("first challan has Tuition + Admission = 8000", all(x["total"] == 8000 for x in p.get("rows", [])), p.get("rows"))
        check("default due date is the 10th", p.get("dueDate") == iso(m0.replace(day=10)), p.get("dueDate"))
        check("preview writes nothing", vouchers(ca, a, sectionId=sec)["total"] == 0)

        s, r = gen(ca, a, m0, section=sec, issue=issue0, due=due0)
        check("generate creates 3", s == 200 and r["data"]["created"] == 3, (s, r))
        lst = vouchers(ca, a, sectionId=sec, month=iso(m0))
        check("voucher numbers follow the campus code", lst["total"] == 3
              and all(_re.fullmatch(rf"{_re.escape(code)}-F-\d{{6}}", v["voucherNo"]) for v in lst["items"]), lst["items"][:1])
        check("due date as given", all(v["dueDate"] == iso(due0) for v in lst["items"]), lst["items"][:1])
        first = {v["studentId"]: v for v in lst["items"]}

        s, r = gen(ca, a, m0, section=sec, issue=issue0, due=due0)
        p = r.get("data") or {}
        check("rerun creates none", s == 200 and p.get("created") == 0 and p.get("skipped") == 3, (s, r))
        s, r = gen(ca, a, m0, preview=True, section=sec)
        check("rerun preview says why", all(x.get("skippedReason") == "Challan already exists for this month." for x in r["data"]["rows"]), r)

        # next month: arrears with fine, previous challan carried forward
        s, r = gen(ca, a, m1, preview=True, section=sec)
        row = next(x for x in r["data"]["rows"] if x["studentId"] == studs[0])
        check("next month: Tuition only + arrears 8300 (8000 + fine)", [l["description"] for l in row["lines"]] == ["Tuition"]
              and row["arrears"] == 8300 and row["total"] == 11300, row)
        s, r = gen(ca, a, m1, section=sec)
        check("next month generated", s == 200 and r["data"]["created"] == 3, (s, r))
        new = next(v for v in vouchers(ca, a, sectionId=sec, month=iso(m1))["items"] if v["studentId"] == studs[0])
        old = voucher(ca, a, first[studs[0]]["id"])
        check("old challan carried forward to the new one", old["voucher"]["status"] == "CarriedForward"
              and old.get("carriedToVoucherId") == new["id"], old["voucher"])
        nd = voucher(ca, a, new["id"])
        arrears = [l for l in nd["lines"] if l["kind"] == "Arrears"]
        check("arrears line names the month", len(arrears) == 1 and arrears[0]["description"] == f"Arrears ({m0.strftime('%b %Y')})"
              and arrears[0]["net"] == 8300, nd["lines"])

        # April's one-time head, when April is inside the session
        april = _dt.date(m0.year + (1 if m0.month > 4 else 0), 4, 1)
        if _dt.date.fromisoformat(sess["startDate"]) <= april <= _dt.date.fromisoformat(sess["endDate"]):
            set_amount(hq, w.sess, w.g1, annual, 2000)
            s, r = gen(ca, a, april, preview=True, section=sec)
            row = next(x for x in r["data"]["rows"] if x["studentId"] == studs[1])
            check("April includes Annual Fund", "Annual Fund" in [l["description"] for l in row["lines"]], row)
            set_amount(hq, w.sess, w.g1, annual, None)

        sec2, _ = other_class_section(hq, a, w.g1)
        admit_student(ca, a, sec2)
        s, r = gen(ca, a, m0, preview=True, section=sec2)
        check("class without fees is skipped", s == 200 and all(x.get("skippedReason") == "Fee structure not set for this class."
                                                                 for x in r["data"]["rows"]) and r["data"]["toCreate"] == 0, r)
        outside = add_months(_dt.date.fromisoformat(sess["endDate"]), 2)
        s, r = gen(ca, a, outside, preview=True, section=sec)
        check("month outside the session -> 400", s == 400 and r.get("message") == "The month is outside the current session.", (s, r))

        # snapshot: later structure change leaves issued challans alone
        set_amount(hq, w.sess, w.g1, w.tuition, 4000)
        s4 = admit_student(ca, a, sec)["student"]["id"]
        s, r = call("POST", "/fees/vouchers", {"studentId": s4, "month": iso(m0), "issueDate": iso(today()), "dueDate": None},
                    token=ca, campus=a)
        check("single challan for a new admission uses 4000 + Admission", s == 200
              and lines_of(r["data"]) == {"Tuition": 4000, "Admission": 5000}, (s, r))
        check("voucher_snapshot_survives_structure_change", lines_of(old)["Tuition"] == 3000
              and lines_of(voucher(ca, a, first[studs[1]]["id"]))["Tuition"] == 3000, old["lines"])
        s, r = call("POST", "/fees/vouchers", {"studentId": s4, "month": iso(m0), "issueDate": iso(today()), "dueDate": None},
                    token=ca, campus=a)
        check("second single challan for the same month -> 400", s == 400 and r.get("message") == "Challan already exists for this month.", (s, r))
        set_amount(hq, w.sess, w.g1, w.tuition, 3000)

        # two generate calls at once still give one challan per student
        sec3, _ = qa_section(hq, a)
        pair = [admit_student(ca, a, sec3)["student"]["id"] for _ in range(2)]
        out = []
        threads = [threading.Thread(target=lambda: out.append(gen(ca, a, m0, section=sec3))) for _ in range(2)]
        [t.start() for t in threads]
        [t.join() for t in threads]
        got = vouchers(ca, a, sectionId=sec3, month=iso(m0))["items"]
        check("double_generate_one_voucher", sorted(v["studentId"] for v in got) == sorted(pair), ([o[0] for o in out], got))
    finally:
        w.close()


# ------------------------------------------------------------------ vouchers (cancel, adjustment, print)

def pay(token, campus, vid, amount, paid_on=None, method="Cash", reference=None):
    return call("POST", "/fees/payments", {"voucherId": vid, "amount": amount, "paidOn": iso(paid_on or today()),
                                           "method": method, "reference": reference}, token=token, campus=campus)


def single(token, campus, student, month, due=None, issue=None):
    s, r = call("POST", "/fees/vouchers", {"studentId": student, "month": iso(month), "issueDate": iso(issue or today()),
                                           "dueDate": iso(due) if due else None}, token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"single voucher: {s} {r}")
    return r["data"]


def cancel(token, campus, vid, reason="QA cancel"):
    return call("POST", f"/fees/vouchers/{vid}/cancel", {"reason": reason}, token=token, campus=campus)


@group
def vouchers_group(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    try:
        call("PUT", "/fees/settings", settings_body({"bankName": "QA Bank", "accountTitle": "QA School", "accountNo": "0001"}),
             token=ca, campus=a)
        sec, _ = qa_section(hq, a)
        sa = admit_student(ca, a, sec)["student"]["id"]
        sb = admit_student(ca, a, sec)["student"]["id"]
        m0, m1 = month_start(today()), add_months(month_start(today()), 1)
        due = today() + _dt.timedelta(days=3)

        # plain cancel of the latest unpaid challan, then issue again
        v = single(ca, a, sb, m0, due=due)
        s, r = cancel(ca, a, v["voucher"]["id"])
        check("cancel the latest unpaid challan", s == 200 and voucher(ca, a, v["voucher"]["id"])["voucher"]["status"] == "Cancelled", (s, r))
        check("cancel keeps the reason", voucher(ca, a, v["voucher"]["id"]).get("cancelReason") == "QA cancel")
        vb = single(ca, a, sb, m0, due=due)
        check("the month can be issued again", vb["voucher"]["status"] == "Unpaid" and vb["voucher"]["total"] == 8000, vb["voucher"])

        # cancel_restores_partial_status
        va = single(ca, a, sa, m0, due=due)
        pay(ca, a, va["voucher"]["id"], 1000)
        vn = single(ca, a, sa, m1, due=due)
        check("next challan carries 7000", lines_of(vn).get(f"Arrears ({m0.strftime('%b %Y')})") == 7000, vn["lines"])
        s, r = cancel(ca, a, va["voucher"]["id"])
        check("carried challan cannot be cancelled", s == 400 and r.get("message") == "Only the latest challan of a student can be cancelled.", (s, r))
        s, r = cancel(ca, a, vn["voucher"]["id"])
        back = voucher(ca, a, va["voucher"]["id"])
        check("cancel_restores_partial_status", s == 200 and back["voucher"]["status"] == "PartiallyPaid"
              and back["voucher"]["balance"] == 7000 and back.get("carriedToVoucherId") is None, (s, back["voucher"]))
        again = single(ca, a, sa, m1, due=due)
        check("regenerated month carries the same arrears", lines_of(again).get(f"Arrears ({m0.strftime('%b %Y')})") == 7000, again["lines"])

        # cancel with a payment, adjustments
        vbid = vb["voucher"]["id"]
        pay(ca, a, vbid, 500)
        s, r = cancel(ca, a, vbid)
        check("cancel with payments -> 400", s == 400 and r.get("message") == "This challan has payments. Void them first.", (s, r))
        s, r = call("POST", f"/fees/vouchers/{vbid}/adjustments", {"amount": -500, "reason": "QA waiver"}, token=ca, campus=a)
        check("adjustment -500", s == 200 and r["data"]["voucher"]["total"] == 7500 and lines_of(r["data"]).get("QA waiver") == -500, (s, r))
        s, r = call("POST", f"/fees/vouchers/{vbid}/adjustments", {"amount": -7200, "reason": "QA too much"}, token=ca, campus=a)
        check("adjustment below paid -> 400", s == 400 and r.get("message") == "Adjustment would take the total below the amount already paid.", (s, r))
        s, r = call("POST", f"/fees/vouchers/{va['voucher']['id']}/adjustments", {"amount": 100, "reason": "QA"}, token=ca, campus=a)
        check("adjustment on a carried challan -> closed", s == 400 and r.get("message") == "This challan is closed.", (s, r))

        prints = data(call("GET", f"/fees/vouchers/print?month={iso(m0)}&sectionId={sec}", token=ca, campus=a))
        check("print: one per non-cancelled challan with bank details", len(prints) == 2
              and all(p["bankName"] == "QA Bank" and p["accountNo"] == "0001" for p in prints)
              and {p["voucher"]["studentId"] for p in prints} == {sa, sb}, [p["voucher"]["voucherNo"] for p in prints])
        one = data(call("GET", f"/fees/vouchers/print?ids={again['voucher']['id']}", token=ca, campus=a))
        check("print by id", len(one) == 1 and one[0]["voucher"]["id"] == again["voucher"]["id"] and len(one[0]["lines"]) == 2, one)
    finally:
        call("DELETE", "/fees/settings", token=ca, campus=a)
        w.close()


GROUPS["vouchers"] = GROUPS.pop("vouchers-group")


# ------------------------------------------------------------------ payments

@group
def payments(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    try:
        code = campus_code_of(hq, a)
        m0, m1 = month_start(today()), add_months(month_start(today()), 1)
        sec, _ = qa_section(hq, a)
        studs = [admit_student(ca, a, sec) for _ in range(2)]
        gen(ca, a, m0, section=sec, due=today() + _dt.timedelta(days=5), issue=today())
        s1, s2 = studs[0]["student"], studs[1]["student"]
        look = data(call("GET", f"/fees/collect/lookup?q={s1['admissionNo']}", token=ca, campus=a))
        hit = look[0] if look else {}
        check("lookup by admission no", len(look) == 1 and hit.get("openVoucher") and hit.get("fineIfPaidToday") == 0, look)
        v1 = hit["openVoucher"]["voucher"]["id"]

        s, r = pay(ca, a, v1, 8000, method="Bank", reference="QA-BR-1")
        p1 = r.get("data") or {}
        check("full payment", s == 200 and _re.fullmatch(rf"{_re.escape(code)}-R-\d{{6}}", p1.get("receiptNo", ""))
              and p1.get("method") == "Bank" and p1.get("reference") == "QA-BR-1", (s, r))
        check("challan paid", voucher(ca, a, v1)["voucher"]["status"] == "Paid")
        s, r = pay(ca, a, v1, 1)
        check("paying a paid challan -> closed", s == 400 and r.get("message") == "This challan is closed.", (s, r))

        v2 = vouchers(ca, a, sectionId=sec, month=iso(m0))["items"]
        v2 = next(v for v in v2 if v["studentId"] == s2["id"])["id"]
        pay(ca, a, v2, 2000)
        d2 = voucher(ca, a, v2)["voucher"]
        check("partial payment", (d2["status"], d2["balance"]) == ("PartiallyPaid", 6000), d2)
        s, r = pay(ca, a, v2, 6500)
        check("more than balance -> 400", s == 400 and r.get("message") == "Amount is more than the balance of Rs 6,000.", (s, r))
        s, r = pay(ca, a, v2, 10, paid_on=today() + _dt.timedelta(days=1))
        check("future date -> 400", s == 400, (s, r))

        # late payments: fine once
        sec2, _ = qa_section(hq, a)
        late = [admit_student(ca, a, sec2)["student"]["id"] for _ in range(2)]
        yday = today() - _dt.timedelta(days=1)
        gen(ca, a, m0, section=sec2, due=yday, issue=yday - _dt.timedelta(days=5))
        lv = {v["studentId"]: v["id"] for v in vouchers(ca, a, sectionId=sec2, month=iso(m0))["items"]}
        look = data(call("GET", f"/fees/collect/lookup?q={voucher(ca, a, lv[late[0]])['voucher']['voucherNo']}", token=ca, campus=a))
        check("lookup by challan no shows the fine", look and look[0]["fineIfPaidToday"] == 300, look)
        s, r = pay(ca, a, lv[late[0]], 1000)
        d = voucher(ca, a, lv[late[0]])
        check("late payment adds the fine", s == 200 and d["voucher"]["total"] == 8300 and lines_of(d).get("Late fine") == 300, d["voucher"])
        pay(ca, a, lv[late[0]], 500)
        d = voucher(ca, a, lv[late[0]])
        check("second late payment adds no more fine", [l["kind"] for l in d["lines"]].count("LateFine") == 1
              and d["voucher"]["balance"] == 6800, d["lines"])
        gen(ca, a, m1, section=sec2)
        nxt = {v["studentId"]: v["id"] for v in vouchers(ca, a, sectionId=sec2, month=iso(m1))["items"]}
        arr = f"Arrears ({m0.strftime('%b %Y')})"
        check("fine_once_across_payment_and_carry", lines_of(voucher(ca, a, nxt[late[0]])).get(arr) == 6800
              and lines_of(voucher(ca, a, nxt[late[1]])).get(arr) == 8300,
              (voucher(ca, a, nxt[late[0]])["lines"], voucher(ca, a, nxt[late[1]])["lines"]))

        # void
        s, r = call("POST", f"/fees/payments/{p1['id']}/void", {"reason": "QA wrong"}, token=ca, campus=a)
        d1 = voucher(ca, a, v1)["voucher"]
        check("void puts the challan back", s == 200 and (d1["status"], d1["paid"]) == ("Unpaid", 0), (s, d1))
        late_pay = voucher(ca, a, lv[late[0]])["payments"][0]["id"]
        s, r = call("POST", f"/fees/payments/{late_pay}/void", {"reason": "QA"}, token=ca, campus=a)
        check("void on a carried challan -> closed", s == 400 and r.get("message") == "This challan is closed.", (s, r))
        rec = data(call("GET", f"/fees/payments/{p1['id']}/receipt", token=ca, campus=a))
        check("receipt", rec["payment"]["receiptNo"] == p1["receiptNo"] and rec["studentName"] == s1["firstName"] + " " + s1["lastName"], rec)
        todays = data(call("GET", "/fees/payments/today", token=ca, campus=a))
        check("today's receipts include ours", {p1["id"], late_pay} <= {p["id"] for p in todays}, len(todays))
    finally:
        w.close()


# ------------------------------------------------------------------ defaulters, reports, student fees, dashboard

def monthly(token, campus, month):
    return data(call("GET", f"/fees/reports/monthly?month={iso(month)}", token=token, campus=campus))["totals"]


def heads_report(token, campus, month):
    return {r["head"]: r for r in data(call("GET", f"/fees/reports/heads?month={iso(month)}", token=token, campus=campus))["rows"]}


def collection(token, campus, d):
    r = data(call("GET", f"/fees/reports/collection?from={iso(d)}&to={iso(d)}", token=token, campus=campus))
    return r["total"], r["voided"], {m["method"]: m["total"] for m in r["byMethod"]}


@group
def reports(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    try:
        m0, m1 = month_start(today()), add_months(month_start(today()), 1)
        yday = today() - _dt.timedelta(days=1)
        sec, _ = qa_section(hq, a)
        s1, s2, s3 = (admit_student(ca, a, sec)["student"]["id"] for _ in range(3))
        before_m, before_h, before_c = monthly(ca, a, m0), heads_report(ca, a, m0), collection(ca, a, today())
        cid = concession(ca, a, s3, w.sess, w.tuition, value=10, reason="QA report")[1]["data"]["id"]
        gen(ca, a, m0, section=sec, due=yday, issue=yday - _dt.timedelta(days=5))
        v = {x["studentId"]: x["id"] for x in vouchers(ca, a, sectionId=sec, month=iso(m0))["items"]}
        pay(ca, a, v[s1], 8300)
        pay(ca, a, v[s2], 1000, method="Bank", reference="QA-1")
        _, r = pay(ca, a, v[s3], 100)
        call("POST", f"/fees/payments/{r['data']['id']}/void", {"reason": "QA"}, token=ca, campus=a)

        after_m = monthly(ca, a, m0)
        delta = {k: round(after_m[k] - before_m[k], 2) for k in ("vouchers", "billed", "concession", "collected", "outstanding")}
        check("monthly totals move by our challans", delta == {"vouchers": 3, "billed": 24000, "concession": 300, "collected": 9300,
                                                               "outstanding": 15300}, delta)  # open balances: s2 7300 + s3 8000 (incl. the fine)
        check("collection % has 2 decimals", after_m["collectionPercent"] == round(after_m["collected"] * 100 / max(after_m["billed"] - after_m["concession"], 1), 2)
              or after_m["billed"] == after_m["concession"], after_m)
        after_h = heads_report(ca, a, m0)
        dt = round(after_h["Tuition"]["amount"] - before_h.get("Tuition", {"amount": 0})["amount"], 2)
        dc = round(after_h["Tuition"]["concession"] - before_h.get("Tuition", {"concession": 0})["concession"], 2)
        check("heads report: Tuition 9000 with 300 off", (dt, dc) == (9000, 300), (dt, dc))
        t, vd, by = collection(ca, a, today())
        check("collection report: totals by method, void kept apart",
              (round(t - before_c[0], 2), round(vd - before_c[1], 2), by.get("Cash", 0) - before_c[2].get("Cash", 0),
               by.get("Bank", 0) - before_c[2].get("Bank", 0)) == (9300, 100, 8300, 1000), (t, vd, by, before_c))

        d = data(call("GET", f"/fees/defaulters?sectionId={sec}", token=ca, campus=a))
        got = {x["studentId"]: (x["months"], x["balance"]) for x in d["items"]}
        check("defaulters: past-due open challans only (s3 keeps the fine its voided late payment added)", got == {s2: (1, 7300), s3: (1, 8000)}, got)

        gen(ca, a, m1, section=sec, due=yday, issue=yday - _dt.timedelta(days=5))
        d = data(call("GET", f"/fees/defaulters?sectionId={sec}&minMonths=2", token=ca, campus=a))
        got = {x["studentId"]: x["months"] for x in d["items"]}
        check("after a carry the months count 2", got == {s2: 2, s3: 2}, d["items"])
        s, raw, _ = download(f"/fees/defaulters/csv?sectionId={sec}", ca, campus=a)
        head = raw.decode("utf-8-sig").splitlines()[0] if s == 200 and raw else ""
        check("defaulters CSV header", head == "Admission no,Name,Class,Section,Guardian phone,Months,Balance", (s, raw[:120]))

        s, r = call("POST", f"/students/{s3}/leave", {"leftOn": iso(today()), "reason": "QA left"}, token=ca, campus=a)
        d = data(call("GET", f"/fees/defaulters?sectionId={sec}", token=ca, campus=a))
        left = next((x for x in d["items"] if x["studentId"] == s3), None)
        check("left_student_still_defaulter", s == 200 and left and left["studentLeft"] is True, (s, left))
        open3 = next(x for x in vouchers(ca, a, sectionId=sec, month=iso(m1))["items"] if x["studentId"] == s3)
        s, r = pay(ca, a, open3["id"], 100)
        check("a left student's balance can still be collected", s == 200, (s, r))

        sf = data(call("GET", f"/students/{s2}/fees", token=ca, campus=a))
        open2 = next(x for x in sf["vouchers"] if x["status"] in ("Unpaid", "PartiallyPaid"))
        check("student fees: balance is the open challan's", sf["balance"] == open2["balance"] and len(sf["vouchers"]) == 2, sf)
        check("student fees: payments listed", [p["amount"] for p in sf["payments"]] == [1000], sf["payments"])

        kpis = data(call("GET", "/dashboard", token=ca, campus=a))["kpis"]
        k = next((x for x in kpis if x["id"] == "fees"), None)
        check("fees KPI", k and _re.fullmatch(r"Rs [\d,]+", k["value"]) and _re.fullmatch(r"Rs [\d,]+ outstanding", k.get("hint") or "")
              and k.get("href") == "/admin/fees/reports", k)
        _, e, p = make_user(hq, campus_ids=[a], role_names=["Teacher"])
        tok = login(e, p)["tokens"]["accessToken"]
        kpis = data(call("GET", "/dashboard", token=tok, campus=a))["kpis"]
        check("no fees KPI for a Teacher", not any(x["id"] == "fees" for x in kpis), [x["id"] for x in kpis])
        call("DELETE", f"/fees/concessions/{cid}", token=ca, campus=a)
    finally:
        w.close()


# ------------------------------------------------------------------ isolation and permissions

def role_token(hq, campus, role):
    _, e, p = make_user(hq, campus_ids=[campus], role_names=[role])
    return login(e, p)["tokens"]["accessToken"]


@group
def fees_isolation(hq):
    from attendance_e2e import qa_section
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, b)
    try:
        m0 = month_start(today())
        bsec, _ = qa_section(hq, b)
        bs = admit_student(hq, b, bsec)
        bsid = bs["student"]["id"]
        bv = single(hq, b, bsid, m0, due=today() + _dt.timedelta(days=5))
        bvid = bv["voucher"]["id"]
        _, bp = pay(hq, b, bvid, 100)
        bpid = bp["data"]["id"]
        bc = concession(hq, b, bsid, w.sess, w.tuition, value=5, reason="QA iso")[1]["data"]["id"]
        _, bh = call("POST", "/fees/heads", {"name": "QA B head " + rnd(4), "frequency": "Monthly", "oneTimeMonth": None,
                                             "sortOrder": 9, "isActive": True, "mainCampus": False}, token=hq, campus=b)
        bhid = bh["data"]["id"]
        attempts = [
            ("voucher GET", call("GET", f"/fees/vouchers/{bvid}", token=ca, campus=a)[0]),
            ("voucher cancel", call("POST", f"/fees/vouchers/{bvid}/cancel", {"reason": "x"}, token=ca, campus=a)[0]),
            ("voucher adjust", call("POST", f"/fees/vouchers/{bvid}/adjustments", {"amount": -1, "reason": "x"}, token=ca, campus=a)[0]),
            ("payment on B voucher", pay(ca, a, bvid, 1)[0]),
            ("payment void", call("POST", f"/fees/payments/{bpid}/void", {"reason": "x"}, token=ca, campus=a)[0]),
            ("receipt", call("GET", f"/fees/payments/{bpid}/receipt", token=ca, campus=a)[0]),
            ("concession PUT", call("PUT", f"/fees/concessions/{bc}", {"studentId": bsid, "sessionId": w.sess, "feeHeadId": w.tuition,
                                                                      "kind": "Manual", "staffId": None, "valueType": "Percent",
                                                                      "value": 1, "reason": "x"}, token=ca, campus=a)[0]),
            ("concession DELETE", call("DELETE", f"/fees/concessions/{bc}", token=ca, campus=a)[0]),
            ("concession for a B student", concession(ca, a, bsid, w.sess, w.tuition)[0]),
            ("preview of a B student", call("GET", f"/fees/concessions/preview?studentId={bsid}&month={iso(m0)}", token=ca, campus=a)[0]),
            ("single challan for a B student", call("POST", "/fees/vouchers", {"studentId": bsid, "month": iso(m0), "issueDate": iso(today()),
                                                                              "dueDate": None}, token=ca, campus=a)[0]),
            ("student fees", call("GET", f"/students/{bsid}/fees", token=ca, campus=a)[0]),
            ("B head edit", call("PUT", f"/fees/heads/{bhid}", {"name": "x", "frequency": "Monthly", "oneTimeMonth": None, "sortOrder": 1,
                                                               "isActive": True, "mainCampus": False}, token=ca, campus=a)[0]),
        ]
        check("campus A gets 404 for campus B fee data", all(s == 404 for _, s in attempts), [(n, s) for n, s in attempts if s != 404])
        check("A's challan list has no B challans", all(v["id"] != bvid for v in vouchers(ca, a, month=iso(m0))["items"]))
        prints = data(call("GET", f"/fees/vouchers/print?ids={bvid}", token=ca, campus=a))
        check("print skips B challans", prints == [], prints)
        look = data(call("GET", f"/fees/collect/lookup?q={bs['student']['admissionNo']}", token=ca, campus=a))
        check("lookup finds no B student", look == [], look)
        today_a = data(call("GET", "/fees/payments/today", token=ca, campus=a))
        check("today's receipts have no B payment", all(p["id"] != bpid for p in today_a))
        s, r = call("GET", "/fees/vouchers", token=ca, campus=b)
        check("A's admin cannot switch to campus B", s == 403, s)
        call("POST", f"/fees/payments/{bpid}/void", {"reason": "QA"}, token=hq, campus=b)
        cancel(hq, b, bvid)
        call("DELETE", f"/fees/concessions/{bc}", token=hq, campus=b)
        call("DELETE", f"/fees/heads/{bhid}", token=hq, campus=b)
    finally:
        w.close()


@group
def fees_permissions(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    w = FeeWorld(hq, a)
    try:
        m0 = month_start(today())
        sec, _ = qa_section(hq, a)
        sid = admit_student(hq, a, sec)["student"]["id"]
        v = single(hq, a, sid, m0, due=today() + _dt.timedelta(days=5))
        vid = v["voucher"]["id"]
        _, p = pay(hq, a, vid, 50)
        pid = p["data"]["id"]
        gen_body = {"month": iso(m0), "classId": None, "sectionId": sec, "issueDate": iso(today()), "dueDate": None, "preview": False}

        def probe(tok):
            return {
                "heads": call("GET", "/fees/heads", token=tok, campus=a)[0],
                "structure edit": set_amount(tok, w.sess, w.g1, w.tuition, 1, campus=a, main=False)[0],
                "vouchers": call("GET", "/fees/vouchers", token=tok, campus=a)[0],
                "generate": call("POST", "/fees/vouchers/generate", gen_body, token=tok, campus=a)[0],
                "lookup": call("GET", "/fees/collect/lookup?q=Qa", token=tok, campus=a)[0],
                "receive": pay(tok, a, vid, 1)[0],
                "defaulters": call("GET", "/fees/defaulters", token=tok, campus=a)[0],
            }

        teacher = probe(role_token(hq, a, "Teacher"))
        check("Teacher: 403 everywhere in fees", all(s == 403 for s in teacher.values()), teacher)
        clerk_tok = role_token(hq, a, "Clerk")
        clerk = probe(clerk_tok)
        check("Clerk: view challans and take payments, nothing else",
              (clerk["vouchers"], clerk["lookup"], clerk["receive"], clerk["generate"], clerk["structure edit"], clerk["defaulters"])
              == (200, 200, 200, 403, 403, 403), clerk)
        s, _ = call("POST", f"/fees/payments/{pid}/void", {"reason": "x"}, token=clerk_tok, campus=a)
        check("Clerk cannot void", s == 403, s)
        acct = role_token(hq, a, "Accountant")
        s1 = call("GET", "/fees/defaulters", token=acct, campus=a)[0]
        s2 = call("POST", "/fees/vouchers/generate", dict(gen_body, preview=True), token=acct, campus=a)[0]
        s3 = call("POST", f"/fees/payments/{pid}/void", {"reason": "QA"}, token=acct, campus=a)[0]
        check("Accountant: reports, generate, void", (s1, s2, s3) == (200, 200, 200), (s1, s2, s3))
        prin = role_token(hq, a, "Principal")
        check("Principal: setup and reports", call("GET", f"/fees/structure?sessionId={w.sess}", token=prin, campus=a)[0] == 200
              and call("GET", f"/fees/reports/monthly?month={iso(m0)}", token=prin, campus=a)[0] == 200)
        set_amount(hq, w.sess, w.g1, w.tuition, None, campus=a, main=False)
        for x in voucher(hq, a, vid)["payments"]:
            if not x["isVoid"]:
                call("POST", f"/fees/payments/{x['id']}/void", {"reason": "QA"}, token=hq, campus=a)
        cancel(hq, a, vid)
    finally:
        w.close()


# ------------------------------------------------------------------ final review fixes

@group
def review_fixes(hq):
    from attendance_e2e import qa_section
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = FeeWorld(hq, a)
    try:
        m0, m1 = month_start(today()), add_months(month_start(today()), 1)
        later = today() + _dt.timedelta(days=5)
        sec, _ = qa_section(hq, a)
        s1, s2, s3, s4 = (admit_student(ca, a, sec)["student"]["id"] for _ in range(4))

        # 1. never two open challans
        v0 = single(ca, a, s1, m0, due=later)
        _, p = pay(ca, a, v0["voucher"]["id"], 8000)
        single(ca, a, s1, m1, due=later)
        s, r = call("POST", f"/fees/payments/{p['data']['id']}/void", {"reason": "QA"}, token=ca, campus=a)
        check("void blocked when a later challan exists", s == 400 and r.get("message") == "A later challan exists. Cancel it first.", (s, r))
        single(ca, a, s2, m1, due=later)
        s, r = call("POST", "/fees/vouchers", {"studentId": s2, "month": iso(m0), "issueDate": iso(today()), "dueDate": None}, token=ca, campus=a)
        check("no challan for a month before an existing one", s == 400 and r.get("message") == "A later challan already exists.", (s, r))
        s, r = gen(ca, a, m0, preview=True, section=sec)
        row = next(x for x in r["data"]["rows"] if x["studentId"] == s2)
        check("generate skips a month before an existing challan", row.get("skippedReason") == "A later challan already exists.", row)

        # 2. two payments at once on one challan
        v3 = single(ca, a, s3, m0, due=later)["voucher"]["id"]
        out = []
        ts = [threading.Thread(target=lambda: out.append(pay(ca, a, v3, 8000)[0])) for _ in range(2)]
        [t.start() for t in ts]
        [t.join() for t in ts]
        d = voucher(ca, a, v3)
        live = [x for x in d["payments"] if not x["isVoid"]]
        check("concurrent payments: one wins, totals stay true", sorted(out) in ([200, 400], [200, 409]) and len(live) == 1
              and d["voucher"]["paid"] == 8000, (out, d["voucher"], len(live)))

        # 3. monthly report: carried balance leaves the old month
        before0, before1 = monthly(ca, a, m0), monthly(ca, a, m1)
        single(ca, a, s4, m0, due=later)
        mid = monthly(ca, a, m0)
        v41 = single(ca, a, s4, m1, due=later)
        after0 = monthly(ca, a, m0)
        pay(ca, a, v41["voucher"]["id"], v41["voucher"]["balance"])
        after1 = monthly(ca, a, m1)
        check("unpaid month counts as outstanding", round(mid["outstanding"] - before0["outstanding"], 2) == 8000, (before0, mid))
        check("carried balance is no longer outstanding in the old month", round(after0["outstanding"] - before0["outstanding"], 2) == 0, after0)
        check("paid next month leaves nothing outstanding there", round(after1["outstanding"] - before1["outstanding"], 2) == 0, (before1, after1))

        # 4. teacher has no fee data; 5. accountant can use the lists fees need
        teacher = role_token(hq, a, "Teacher")
        check("teacher cannot read student fees", call("GET", f"/students/{s1}/fees", token=teacher, campus=a)[0] == 403)
        acct = role_token(hq, a, "Accountant")
        got = [call("GET", u, token=acct, campus=a)[0] for u in ("/academic-sessions", "/classes", "/sections", "/students?q=Qa&pageSize=5")]
        check("accountant can load sessions, classes, sections and search students", got == [200, 200, 200, 200], got)

        # 7. campus settings keep per-field fallback
        s, r = call("PUT", "/fees/settings", settings_body({"lateFine": 450}), token=ca, campus=a)
        check("only the changed field is the campus's own", s == 200 and r["data"].get("overriddenFields") == ["lateFine"], (s, r))
    finally:
        call("DELETE", "/fees/settings", token=ca, campus=a)
        w.close()


@group
def long_campus_code(hq):
    from attendance_e2e import qa_section
    from academics_e2e import campus_body
    code = "QA-" + rnd(4) + "-LONG-CAMPUS-CODE-X"
    s, r = call("POST", "/campuses", campus_body(code), token=hq)
    if not check("create a campus with a long code", s == 200, (s, r)):
        return
    c = track("campuses", r["data"]["id"])
    w = FeeWorld(hq, c)
    try:
        s, r = call("POST", "/sections", {"classId": w.g1, "academicSessionId": None, "name": "Q" + rnd(3), "shift": "Morning",
                                          "capacity": None, "room": None, "isActive": True}, token=hq, campus=c)
        sec = track("sections", r["data"]["id"])
        # Admission numbers built from a long campus code overflow too (a students-module issue); give one.
        sid = admit_student(hq, c, sec, admission_no="QA-" + rnd(6))["student"]["id"]
        s, r = call("POST", "/fees/vouchers", {"studentId": sid, "month": iso(month_start(today())), "issueDate": iso(today()), "dueDate": None},
                    token=hq, campus=c)
        check("challan number fits a long campus code", s == 200 and r["data"]["voucher"]["voucherNo"].startswith(code + "-F-"), (s, r))
        if s == 200:
            s2, r2 = pay(hq, c, r["data"]["voucher"]["id"], 100)
            check("receipt number fits a long campus code", s2 == 200 and r2["data"]["receiptNo"].startswith(code + "-R-"), (s2, r2))
            call("POST", f"/fees/payments/{r2['data']['id']}/void", {"reason": "QA"}, token=hq, campus=c)
            cancel(hq, c, r["data"]["voucher"]["id"])
    finally:
        w.close()


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
