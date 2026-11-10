"""End-to-end checks for Exams and Results.

Usage: python tests/e2e/exams_e2e.py <group> [<group> ...]
Runs against the local dev API. Exams with marks cannot be deleted, so groups name theirs "QA ..." and close them.
"""
import datetime as _dt
import random as _random
import re as _re
import sys

from sms_client import (admin_token, call, check, cleanup, data, download, login, make_user, summary, track,
                        wait_for_api, _created)
from academics_e2e import actions, campus_admin, demo_campuses, menu_keys, role_detail

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def rnd(n):
    return "".join(_random.choice("0123456789") for _ in range(n))


DEFAULT_BANDS = [("A+", 90), ("A", 80), ("B", 70), ("C", 60), ("D", 50), ("E", 40), ("F", 0)]


def bands(pairs, fail="F"):
    return [{"grade": g, "minPercent": m, "isFail": g == fail} for g, m in pairs]


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    me = data(call("GET", "/auth/me", token=hq))
    check("menu has Exams after Attendance", menu_keys(me) == ["dashboard", "campuses", "academics", "students-menu", "staff-menu",
                                                              "attendance-menu", "exams-menu", "fees-menu", "settings"], menu_keys(me))
    grp = next((m for m in me["menu"] if m["key"] == "exams-menu"), None)
    check("exams children", grp and [c["key"] for c in grp["children"]] == ["exams", "marks", "results", "grading"], grp)
    full, view = {"view", "add", "edit", "delete"}, {"view"}
    keys = ("exams", "marks", "results", "grading")
    expected = {
        "Principal": {k: full for k in keys},
        "CampusAdmin": {k: full for k in keys},
        "Teacher": {"exams": view, "marks": {"view", "edit"}, "results": view, "grading": set()},
        "Clerk": {"exams": view, "marks": set(), "results": view, "grading": set()},
        "Accountant": {k: set() for k in keys},
    }
    for role, want in expected.items():
        got = {k: actions(role_detail(hq, role), k) for k in keys}
        check(f"{role} exam grants", got == want, got)
    a, _ = demo_campuses(hq)
    scale = data(call("GET", "/grading/scale", token=hq, campus=a))
    check("default scale bands", [(x["grade"], x["minPercent"]) for x in scale["bands"]] == DEFAULT_BANDS
          and [x["isFail"] for x in scale["bands"]] == [False] * 6 + [True], scale)
    sch = data(call("GET", "/grading/schemes", token=hq, campus=a))
    eff = sch["effective"]
    check("default scheme 20/30/50/0, must pass final", (eff["monthlyWeight"], eff["midTermWeight"], eff["finalWeight"],
                                                        eff["otherWeight"], eff["mustPassFinal"]) == (20, 30, 50, 0, True), sch)


# ------------------------------------------------------------------ grading

@group
def grading(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    mine = [("A+", 95), ("A", 85), ("B", 75), ("C", 65), ("D", 55), ("E", 45), ("F", 0)]
    s, r = call("PUT", "/grading/scale", {"name": "QA Scale", "bands": bands(mine), "mainCampus": False}, token=ca, campus=a)
    check("campus saves its own scale", s == 200 and [(x["grade"], x["minPercent"]) for x in r["data"]["bands"]] == mine
          and r["data"].get("campusId") == a, (s, r))
    other = data(call("GET", "/grading/scale", token=hq, campus=b))
    check("other campus keeps the Main Campus scale", [(x["grade"], x["minPercent"]) for x in other["bands"]] == DEFAULT_BANDS, other)
    s, r = call("PUT", "/grading/scale", {"name": "x", "bands": bands([("A", 50), ("F", 10)]), "mainCampus": False}, token=ca, campus=a)
    check("bands without a 0 band -> 400", s == 400, (s, r))
    s, r = call("PUT", "/grading/scale", {"name": "x", "bands": bands([("A", 50), ("A", 0)], fail="A"), "mainCampus": False}, token=ca, campus=a)
    check("duplicate grade -> 400", s == 400, (s, r))
    s, r = call("PUT", "/grading/scale", {"name": "x", "bands": bands(DEFAULT_BANDS), "mainCampus": True}, token=ca, campus=a)
    check("campus admin cannot change the Main Campus scale", s == 403, (s, r))
    # restore the campus to the Main Campus scale
    call("DELETE", "/grading/scale", token=ca, campus=a)
    back = data(call("GET", "/grading/scale", token=ca, campus=a))
    check("deleting the campus scale falls back to Main Campus", [(x["grade"], x["minPercent"]) for x in back["bands"]] == DEFAULT_BANDS, back)

    body = {"sessionId": None, "classId": None, "monthlyWeight": 20, "midTermWeight": 30, "finalWeight": 40, "otherWeight": 0,
            "mustPassFinal": True, "mainCampus": False}
    s, r = call("PUT", "/grading/schemes", body, token=ca, campus=a)
    check("weights not 100 -> 400", s == 400 and r.get("message") == "Weights must add up to 100.", (s, r))
    classes = data(call("GET", "/classes", token=ca, campus=a))
    cid = classes[0]["id"]
    s, r = call("PUT", "/grading/schemes", dict(body, classId=cid, monthlyWeight=0, midTermWeight=40, finalWeight=60), token=ca, campus=a)
    check("class override saved", s == 200, (s, r))
    eff_c = data(call("GET", f"/grading/schemes?classId={cid}", token=ca, campus=a))["effective"]
    eff_o = data(call("GET", f"/grading/schemes?classId={classes[1]['id']}", token=ca, campus=a))["effective"]
    check("class override applies to that class only", (eff_c["midTermWeight"], eff_o["midTermWeight"]) == (40, 30), (eff_c, eff_o))
    sid = r["data"]["id"] if s == 200 else None
    if sid:
        s, _ = call("DELETE", f"/grading/schemes/{sid}", token=ca, campus=a)
        check("delete the class override", s == 200, s)


# ------------------------------------------------------------------ setup helpers

def current_session(token, campus):
    lst = data(call("GET", "/academic-sessions", token=token, campus=campus))
    return next(x for x in lst["items"] if x["id"] == lst["effectiveCurrentId"])


def class_by_code(token, campus, code):
    return next(c for c in data(call("GET", "/classes", token=token, campus=campus)) if c["code"] == code)


def exam_body(name=None, type_="MidTerm", session_id=None, class_ids=(), start=None, end=None, main=False):
    return {"name": name or f"QA {type_} {rnd(4)}", "type": type_, "sessionId": session_id, "startDate": start, "endDate": end,
            "classIds": list(class_ids), "mainCampus": main}


def make_exam(token, campus, class_codes=("G1",), type_="MidTerm", status="Open", **over):
    sess = current_session(token, campus)
    ids = [class_by_code(token, campus, c)["id"] for c in class_codes]
    s, r = call("POST", "/exams", exam_body(type_=type_, session_id=sess["id"], class_ids=ids, **over), token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"make_exam: {s} {r}")
    eid = track("exams", r["data"]["exam"]["id"])
    if status != "Draft":
        call("POST", f"/exams/{eid}/status", {"status": status}, token=token, campus=campus)
    return eid, data(call("GET", f"/exams/{eid}", token=token, campus=campus))


def paper_of(detail, class_code_name, subject_name):
    cls = next(c for c in detail["classes"] if c["className"] == class_code_name)
    return next(p for p in cls["papers"] if p["subjectName"] == subject_name)


# ------------------------------------------------------------------ setup

@group
def setup(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sess = current_session(ca, a)
    g1, g2 = class_by_code(ca, a, "G1"), class_by_code(ca, a, "G2")
    subj1 = data(call("GET", f"/classes/{g1['id']}", token=ca, campus=a))["subjects"]
    s, r = call("POST", "/exams", exam_body(session_id=sess["id"], class_ids=[g1["id"], g2["id"]]), token=ca, campus=a)
    ok = check("create Mid-term for two classes", s == 200 and r["data"]["exam"]["status"] == "Draft", (s, r))
    if not ok:
        return
    eid = track("exams", r["data"]["exam"]["id"])
    det = r["data"]
    c1 = next(c for c in det["classes"] if c["classId"] == g1["id"])
    check("papers follow the class subject list with 100/33", sorted(p["subjectId"] for p in c1["papers"]) == sorted(x["subjectId"] for x in subj1)
          and all(p["totalMarks"] == 100 and p["passMarks"] == 33 for p in c1["papers"]), c1["papers"][:2])
    check("section progress starts at 0", all(x["submitted"] == 0 for x in c1["sections"]) and c1["sections"], c1["sections"])
    s, r = call("POST", "/exams", exam_body(type_="Monthly", session_id=sess["id"], class_ids=[g1["id"]]), token=ca, campus=a)
    if s == 200:
        track("exams", r["data"]["exam"]["id"])
        p = r["data"]["classes"][0]["papers"][0]
        check("monthly papers default 25/9", p["totalMarks"] == 25 and p["passMarks"] == 9, p)
    paper = c1["papers"][0]
    upd = {"totalMarks": 100, "passMarks": 33, "hasPractical": True, "practicalMarks": 25, "paperDate": sess["startDate"], "isActive": True}
    s, r = call("PUT", f"/exams/{eid}/papers/{paper['id']}", upd, token=ca, campus=a)
    check("practical split saved", s == 200 and r["data"]["hasPractical"] and r["data"]["practicalMarks"] == 25, (s, r))
    s, r = call("PUT", f"/exams/{eid}/papers/{paper['id']}", dict(upd, passMarks=120), token=ca, campus=a)
    check("pass above total -> 400", s == 400, (s, r))
    s, r = call("PUT", f"/exams/{eid}/papers/{paper['id']}", dict(upd, practicalMarks=100), token=ca, campus=a)
    check("practical not below total -> 400", s == 400, (s, r))
    s, r = call("POST", "/exams", exam_body(session_id=sess["id"], class_ids=[g1["id"]], start="2001-01-01", end="2001-01-05"), token=ca, campus=a)
    check("dates outside the session -> 400", s == 400, (s, r))
    s, r = call("POST", "/exams", exam_body(name=det["exam"]["name"], session_id=sess["id"], class_ids=[g1["id"]]), token=ca, campus=a)
    check("duplicate name -> 409", s == 409, (s, r))
    for st in ("Open", "Closed", "Open"):
        s, r = call("POST", f"/exams/{eid}/status", {"status": st}, token=ca, campus=a)
        check(f"status -> {st}", s == 200 and r["data"]["status"] == st, (s, r))
    s, r = call("GET", f"/exams?sessionId={sess['id']}", token=ca, campus=a)
    row = next((x for x in r["data"] if x["id"] == eid), None) if s == 200 else None
    check("list shows progress counts", row and row["sectionsTotal"] > 0 and row["sectionsSubmitted"] == 0, row)
    # Main Campus exam: campus admin cannot change its papers
    s, r = call("POST", "/exams", exam_body(session_id=sess["id"], class_ids=[g1["id"]], main=True), token=hq, campus=a)
    if s == 200:
        mid = track("exams", r["data"]["exam"]["id"])
        mp = r["data"]["classes"][0]["papers"][0]
        s2, _ = call("PUT", f"/exams/{mid}/papers/{mp['id']}", upd, token=ca, campus=a)
        check("campus admin cannot edit a Main Campus exam's paper", s2 == 403, s2)
    s, r = call("POST", "/exams", exam_body(session_id=sess["id"], class_ids=[g1["id"]]), token=ca, campus=a)
    s, _ = call("DELETE", f"/exams/{r['data']['exam']['id']}", token=ca, campus=a)
    check("delete an exam without marks", s == 200, s)


# ------------------------------------------------------------------ marks helpers

def exam_world(hq, ca, a, n_students=3, subject_name="Mathematics", class_code="G1", type_="MidTerm"):
    """A QA section with students, a subject teacher (login) for one subject, and an Open exam for the class."""
    from attendance_e2e import qa_section, admit_on, teacher_with_login, today
    from onboarding_e2e import assign
    sec, sdto = qa_section(ca, a, class_code)
    students = [admit_on(ca, a, sec, today() - _dt.timedelta(days=60)) for _ in range(n_students)]
    cls = class_by_code(ca, a, class_code)
    subj = next(s for s in data(call("GET", f"/classes/{cls['id']}", token=ca, campus=a))["subjects"] if s["name"] == subject_name)
    t, tok = teacher_with_login(hq, ca, a)
    assign(ca, a, t, sec, [subj["subjectId"]])
    eid, det = make_exam(ca, a, (class_code,), type_=type_)
    paper = paper_of(det, sdto["className"], subject_name)
    return {"sec": sec, "sdto": sdto, "students": students, "teacher": t, "tok": tok, "exam": eid, "detail": det, "paper": paper,
            "subjectId": subj["subjectId"]}


def marks_url(paper_id, sec):
    return f"/marks/{paper_id}/sections/{sec}"


def save_marks(token, campus, paper_id, sec, rows):
    body = {"rows": [{"studentId": sid, "theory": th, "practical": pr, "status": st} for sid, th, pr, st in rows]}
    return call("PUT", marks_url(paper_id, sec), body, token=token, campus=campus)


# ------------------------------------------------------------------ marks

@group
def marks(hq):
    from onboarding_e2e import retire_staff
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    w = exam_world(hq, ca, a)
    sec, (s1, s2, s3), pid, tok = w["sec"], w["students"], w["paper"]["id"], w["tok"]
    s, r = call("GET", f"/marks/my-papers?examId={w['exam']}", token=tok, campus=a)
    check("teacher my-papers = only the assigned paper", s == 200 and [(x["paperId"], x["sectionId"]) for x in r["data"]] == [(pid, sec)], (s, r))
    s, r = save_marks(tok, a, pid, sec, [(s1, 80, None, "Present"), (s2, None, None, "Absent")])
    check("teacher partial save", s == 200, (s, r))
    sheet = data(call("GET", marks_url(pid, sec), token=tok, campus=a))
    rows = {x["studentId"]: x for x in sheet["rows"]}
    check("sheet shows saved rows, status InProgress, absent stores no marks", sheet["status"] == "InProgress" and rows[s1]["theory"] == 80
          and rows[s2]["status"] == "Absent" and rows[s2].get("theory") is None and rows[s3].get("status") is None, sheet)
    s, r = save_marks(tok, a, pid, sec, [(s1, 101, None, "Present")])
    check("theory above total -> 400", s == 400, (s, r))
    s, r = save_marks(tok, a, pid, sec, [(s1, 50, 10, "Present")])
    check("practical on a non-practical paper -> 400", s == 400, (s, r))
    s, r = call("POST", marks_url(pid, sec) + "/submit", token=tok, campus=a)
    check("submit with a student missing -> 400 with the count", s == 400 and "1" in (r.get("message") or ""), (s, r))
    save_marks(tok, a, pid, sec, [(s3, 33, None, "Present")])
    s, r = call("POST", marks_url(pid, sec) + "/submit", token=tok, campus=a)
    check("submit", s == 200 and r["data"]["status"] == "Submitted", (s, r))
    s, r = save_marks(tok, a, pid, sec, [(s1, 70, None, "Present")])
    check("teacher save after submit -> 400", s == 400 and r.get("message") == "Submitted. Ask an admin to return it.", (s, r))
    s, r = call("POST", marks_url(pid, sec) + "/return", token=tok, campus=a)
    check("teacher cannot return", s == 403, s)
    s, r = call("POST", marks_url(pid, sec) + "/return", token=ca, campus=a)
    check("admin returns to the teacher", s == 200 and r["data"]["status"] == "InProgress", (s, r))
    s, _ = save_marks(tok, a, pid, sec, [(s1, 70, None, "Present")])
    check("teacher can save again after return", s == 200, s)

    from attendance_e2e import teacher_with_login
    t2, tok2 = teacher_with_login(hq, ca, a)
    s, r = save_marks(tok2, a, pid, sec, [(s1, 10, None, "Present")])
    check("unassigned teacher -> 403", s == 403 and r.get("message") == "Only the subject teacher or an admin can enter these marks.", (s, r))
    s, _ = save_marks(ca, a, pid, sec, [(s2, 0, None, "Absent")])
    check("admin can save", s == 200, s)

    # total_below_entered_marks_refused (Task 2 check, needs marks)
    upd = {"totalMarks": 50, "passMarks": 17, "hasPractical": False, "practicalMarks": None, "paperDate": None, "isActive": True}
    s, r = call("PUT", f"/exams/{w['exam']}/papers/{pid}", upd, token=ca, campus=a)
    check("total_below_entered_marks_refused", s == 400, (s, r))

    # ended_assignment_blocks_teacher
    p = data(call("GET", f"/staff/{w['teacher']}", token=ca, campus=a))
    for x in p["assignments"]:
        call("DELETE", f"/staff/{w['teacher']}/assignments/{x['id']}", token=ca, campus=a)
    s, _ = save_marks(tok, a, pid, sec, [(s1, 60, None, "Present")])
    check("ended_assignment_blocks_teacher: teacher 403", s == 403, s)
    s, _ = save_marks(ca, a, pid, sec, [(s1, 60, None, "Present")])
    check("ended_assignment_blocks_teacher: admin 200", s == 200, s)

    # marks_follow_section
    sec2_world_sec, _ = __import__("attendance_e2e").qa_section(ca, a)
    call("POST", f"/students/{s1}/change-section", {"sectionId": sec2_world_sec, "rollNo": None, "reason": "QA"}, token=ca, campus=a)
    old_rows = {x["studentId"]: x for x in data(call("GET", marks_url(pid, sec), token=ca, campus=a))["rows"]}
    new_rows = {x["studentId"]: x for x in data(call("GET", marks_url(pid, sec2_world_sec), token=ca, campus=a))["rows"]}
    check("marks_follow_section: old section keeps the stored row; the new section does not list the student for this paper",
          s1 in old_rows and old_rows[s1]["theory"] == 60 and s1 not in new_rows, (old_rows.get(s1), new_rows.get(s1)))

    call("POST", f"/exams/{w['exam']}/status", {"status": "Draft"}, token=ca, campus=a)
    eid2, det2 = make_exam(ca, a, ("G1",), status="Draft")
    p2 = paper_of(det2, w["sdto"]["className"], "Mathematics")
    s, r = save_marks(ca, a, p2["id"], sec, [(s2, 10, None, "Present")])
    check("Draft exam -> 400", s == 400 and r.get("message") == "Marks can be entered only while the exam is open.", (s, r))
    call("POST", f"/exams/{w['exam']}/status", {"status": "Closed"}, token=ca, campus=a)
    retire_staff(hq)


# ------------------------------------------------------------------ results

def results_world(hq, ca, a):
    """QA section with 6 students; exam with only English, Urdu and Mathematics (75 + 25 practical) active."""
    from attendance_e2e import qa_section, admit_on, today
    sec, sdto = qa_section(ca, a, "G1")
    st = [admit_on(ca, a, sec, today() - _dt.timedelta(days=60)) for _ in range(6)]
    eid, det = make_exam(ca, a, ("G1",))
    cls = next(c for c in det["classes"] if c["className"] == sdto["className"])
    keep = {"English", "Urdu", "Mathematics"}
    papers = {}
    for p in cls["papers"]:
        on = p["subjectName"] in keep
        body = {"totalMarks": 100, "passMarks": 33, "hasPractical": p["subjectName"] == "Mathematics",
                "practicalMarks": 25 if p["subjectName"] == "Mathematics" else None, "paperDate": None, "isActive": on}
        call("PUT", f"/exams/{eid}/papers/{p['id']}", body, token=ca, campus=a)
        if on:
            papers[p["subjectName"]] = p["id"]
    return sec, sdto, st, eid, papers


def tab(token, campus, eid, sec):
    return call("GET", f"/results/{eid}/sections/{sec}", token=token, campus=campus)


@group
def results(hq):
    from attendance_e2e import qa_section, admit_on, teacher_with_login, today
    from onboarding_e2e import retire_staff
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, sdto, (A, B, C, D, E, F), eid, P = results_world(hq, ca, a)
    eng = [(A, 95, None, "Present"), (B, 90, None, "Present"), (C, 85, None, "Present"), (D, 95, None, "Present"), (E, None, None, "Absent")]
    urd = [(A, 90, None, "Present"), (B, 95, None, "Present"), (C, 80, None, "Present"), (D, 20, None, "Present"), (E, None, None, "Exempted")]
    mat = [(A, 70, 25, "Present"), (B, 70, 25, "Present"), (C, 60, 20, "Present"), (D, 70, 25, "Present"), (E, 50, 10, "Present")]
    for subj, rows in (("English", eng), ("Urdu", urd), ("Mathematics", mat)):
        s, r = save_marks(ca, a, P[subj], sec, rows)
        check(f"admin enters {subj}", s == 200, (s, r))

    s, r = tab(ca, a, eid, sec)
    ok = check("live tabulation", s == 200 and not r["data"]["published"], (s, r))
    if not ok:
        return
    rows = {x["studentId"]: x for x in r["data"]["rows"]}
    check("percent and grade: 280/300 -> 93.33 A+", rows[A]["percent"] == 93.33 and rows[A]["grade"] == "A+" and rows[A]["passed"], rows[A])
    check("C: 245/300 -> 81.67 A", rows[C]["percent"] == 81.67 and rows[C]["grade"] == "A", rows[C])
    check("D fails one subject -> overall fail despite 70%", rows[D]["percent"] == 70 and rows[D]["passed"] is False and rows[D].get("position") is None, rows[D])
    check("E: absent counts 0, exempt excluded -> 60/200 = 30% fail", rows[E]["total"] == 200 and rows[E]["obtained"] == 60
          and rows[E]["percent"] == 30 and rows[E]["passed"] is False, rows[E])
    check("positions: tie shares 1, next is 3", (rows[A]["position"], rows[B]["position"], rows[C]["position"]) == (1, 1, 3), rows)
    check("nothing_entered_no_crash: F has no result", rows[F].get("percent") is None and rows[F].get("grade") is None
          and rows[F].get("position") is None, rows[F])
    math_a = next(x for x in rows[A]["subjects"] if x["name"] == "Mathematics")
    check("practical split counted", math_a["theory"] == 70 and math_a["practical"] == 25 and math_a["obtained"] == 95, math_a)

    s, r = call("POST", f"/results/{eid}/sections/{sec}/publish", token=ca, campus=a)
    check("publish with pending papers -> 400 listing them", s == 400 and "English" in (r.get("message") or ""), (s, r))
    for subj in P:
        save_marks(ca, a, P[subj], sec, [(F, None, None, "Exempted")])
        s, r = call("POST", f"/marks/{P[subj]}/sections/{sec}/submit", token=ca, campus=a)
    s, r = call("POST", f"/results/{eid}/sections/{sec}/publish", token=ca, campus=a)
    check("publish", s == 200 and r["data"]["published"], (s, r))
    s, r = save_marks(ca, a, P["English"], sec, [(A, 10, None, "Present")])
    check("marks locked after publish", s == 400 and r.get("message") == "This result is published. Unpublish it first.", (s, r))

    # snapshot_survives_scale_change
    sec2, _ = qa_section(ca, a)
    G = admit_on(ca, a, sec2, today() - _dt.timedelta(days=60))
    save_marks(ca, a, P["English"], sec2, [(G, 94, None, "Present")])
    save_marks(ca, a, P["Urdu"], sec2, [(G, 93, None, "Present")])
    save_marks(ca, a, P["Mathematics"], sec2, [(G, 70, 23, "Present")])
    stricter = [{"grade": g, "minPercent": m, "isFail": g == "F"} for g, m in
                [("A+", 95), ("A", 85), ("B", 75), ("C", 65), ("D", 55), ("E", 45), ("F", 0)]]
    call("PUT", "/grading/scale", {"name": "QA strict", "bands": stricter, "mainCampus": False}, token=ca, campus=a)
    pub = {x["studentId"]: x for x in tab(ca, a, eid, sec)[1]["data"]["rows"]}
    live = {x["studentId"]: x for x in tab(ca, a, eid, sec2)[1]["data"]["rows"]}
    call("DELETE", "/grading/scale", token=ca, campus=a)
    check("snapshot_survives_scale_change", pub[A]["grade"] == "A+" and live[G]["percent"] == 93.33 and live[G]["grade"] == "A", (pub[A], live[G]))

    s, body, headers = download(f"/results/{eid}/sections/{sec}/csv", ca, a)
    check("CSV", s == 200 and body.decode("utf-8-sig").startswith("Roll,Name,"), (s, body[:60]))

    # remarks
    t, tok = teacher_with_login(hq, ca, a)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": t}, token=ca, campus=a)
    sess = current_session(ca, a)
    s, r = call("PUT", "/results/remarks", {"examId": eid, "sessionId": sess["id"], "studentId": A, "remark": "Excellent work"}, token=tok, campus=a)
    check("class teacher saves a remark", s == 200, (s, r))
    t2, tok2 = teacher_with_login(hq, ca, a)
    s, _ = call("PUT", "/results/remarks", {"examId": eid, "sessionId": sess["id"], "studentId": A, "remark": "x"}, token=tok2, campus=a)
    check("another teacher cannot write a remark", s == 403, s)
    call("PUT", f"/sections/{sec}/class-teacher", {"staffId": None}, token=ca, campus=a)

    s, r = call("POST", f"/results/{eid}/sections/{sec}/unpublish", token=ca, campus=a)
    check("unpublish -> live again", s == 200 and not r["data"]["published"], (s, r))
    s, _ = save_marks(ca, a, P["English"], sec, [(A, 95, None, "Present")])
    check("marks editable after unpublish", s == 200, s)
    call("POST", f"/exams/{eid}/status", {"status": "Closed"}, token=ca, campus=a)
    retire_staff(hq)


# ------------------------------------------------------------------ annual / cards / dashboard

def single_paper_exam(ca, a, sdto, type_, **over):
    """An Open exam for G1 with only Mathematics active (100 / 33, no practical); returns (examId, paperId)."""
    eid, det = make_exam(ca, a, ("G1",), type_=type_, **over)
    cls = next(c for c in det["classes"] if c["className"] == sdto["className"])
    pid = None
    for p in cls["papers"]:
        on = p["subjectName"] == "Mathematics"
        call("PUT", f"/exams/{eid}/papers/{p['id']}", {"totalMarks": 100, "passMarks": 33, "hasPractical": False, "practicalMarks": None,
                                                      "paperDate": None, "isActive": on}, token=ca, campus=a)
        if on:
            pid = p["id"]
    return eid, pid


def enter_and_publish(ca, a, eid, pid, sec, rows):
    """rows: [(studentId, marks or 'Absent'/'Exempted')]."""
    body = [(sid, None, None, v) if isinstance(v, str) else (sid, v, None, "Present") for sid, v in rows]
    s1, r1 = save_marks(ca, a, pid, sec, body)
    s2, r2 = call("POST", f"/marks/{pid}/sections/{sec}/submit", token=ca, campus=a)
    s3, r3 = call("POST", f"/results/{eid}/sections/{sec}/publish", token=ca, campus=a)
    if (s1, s2, s3) != (200, 200, 200):
        raise AssertionError(f"enter_and_publish: {s1} {r1} / {s2} {r2} / {s3} {r3}")


@group
def annual(hq):
    from attendance_e2e import qa_section, admit_on, today
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, sdto = qa_section(ca, a, "G1")
    X, Y, Z = [admit_on(ca, a, sec, today() - _dt.timedelta(days=60)) for _ in range(3)]
    plan = [("Monthly", [(X, 60), (Y, "Exempted"), (Z, "Exempted")]),
            ("Monthly", [(X, 80), (Y, "Exempted"), (Z, "Exempted")]),
            ("MidTerm", [(X, 70), (Y, 70), (Z, 70)]),
            ("Final", [(X, 90), (Y, 90), (Z, 30)])]
    exams = []
    for type_, rows in plan:
        eid, pid = single_paper_exam(ca, a, sdto, type_)
        enter_and_publish(ca, a, eid, pid, sec, rows)
        exams.append(eid)
    sess = current_session(ca, a)
    s, r = call("GET", f"/results/annual?sessionId={sess['id']}&sectionId={sec}", token=ca, campus=a)
    ok = check("annual results", s == 200 and len(r["data"]) == 3, (s, r))
    if ok:
        rows = {x["studentId"]: x for x in r["data"]}
        rx, ry, rz = rows[X]["result"], rows[Y]["result"], rows[Z]["result"]
        check("monthly averaged: 70*.2 + 70*.3 + 90*.5 = 80", rx["percent"] == 80 and rx["typePercents"]["Monthly"] == 70 and rx["passed"], rx)
        check("no monthly -> weights rescaled to 37.5 / 62.5 -> 82.5",
              ry["percent"] == 82.5 and ry["effectiveWeights"] == {"MidTerm": 37.5, "Final": 62.5}, ry)
        check("must pass the final: 45% but failed final -> not passed", rz["percent"] == 45 and rz["passed"] is False, rz)
        check("exams listed per student", {e["examId"] for e in rows[X]["exams"]} >= set(exams), rows[X]["exams"])
    for eid in exams:
        call("POST", f"/exams/{eid}/status", {"status": "Closed"}, token=ca, campus=a)


@group
def cards(hq):
    from attendance_e2e import qa_section, admit_on, today, working_days_back, save
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, sdto = qa_section(ca, a, "G1")
    X, Y = [admit_on(ca, a, sec, today() - _dt.timedelta(days=60)) for _ in range(2)]
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    save(ca, a, sec, d, {X: "Present", Y: "Absent"})
    sess = current_session(ca, a)
    eid, pid = single_paper_exam(ca, a, sdto, "MidTerm")
    save_marks(ca, a, pid, sec, [(X, 88, None, "Present"), (Y, 50, None, "Present")])
    s, r = call("GET", f"/report-cards/exam/{eid}?sectionId={sec}", token=ca, campus=a)
    check("unpublished exam -> 400", s == 400 and r.get("message") == "Publish the result first.", (s, r))
    call("POST", f"/marks/{pid}/sections/{sec}/submit", token=ca, campus=a)
    call("POST", f"/results/{eid}/sections/{sec}/publish", token=ca, campus=a)
    call("PUT", "/results/remarks", {"examId": eid, "sessionId": sess["id"], "studentId": X, "remark": "Keep it up"}, token=ca, campus=a)
    s, r = call("GET", f"/report-cards/exam/{eid}?sectionId={sec}", token=ca, campus=a)
    ok = check("exam cards for a section", s == 200 and len(r["data"]) == 2, (s, r))
    if ok:
        cx = next(c for c in r["data"] if c["student"]["studentId"] == X)
        check("card content", len(cx["subjects"]) == 1 and cx["percent"] == 88 and cx["grade"] and cx["position"] == 1
              and cx["attendancePct"] == 100 and cx["remark"] == "Keep it up" and cx["schoolName"] and cx["sessionName"] == sess["name"], cx)
    s, r = call("GET", f"/report-cards/exam/{eid}?studentId={Y}", token=ca, campus=a)
    check("one student's card", s == 200 and len(r["data"]) == 1 and r["data"][0]["attendancePct"] == 0, (s, r))
    s, r = call("GET", f"/report-cards/annual?sessionId={sess['id']}&sectionId={sec}", token=ca, campus=a)
    check("annual cards: one per student with the exams list", s == 200 and len(r["data"]) == 2
          and all(c["annual"] and any(e["examId"] == eid for e in c["exams"]) for c in r["data"]), (s, r))
    other, opid = single_paper_exam(ca, a, sdto, "Monthly")
    save_marks(ca, a, opid, sec, [(X, 40, None, "Present")])
    s, r = call("GET", f"/students/{X}/results", token=ca, campus=a)
    ids = [e["examId"] for e in r["data"]["exams"]] if s == 200 else []
    check("student results list published exams only", eid in ids and other not in ids and r["data"]["annual"], (s, r))
    for e in (eid, other):
        call("POST", f"/exams/{e}/status", {"status": "Closed"}, token=ca, campus=a)


@group
def dashboard_exams(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    make_exam(ca, a, ("G1",))
    kpis = data(call("GET", "/dashboard", token=ca, campus=a))["kpis"]
    k = next((x for x in kpis if x["id"] == "exams"), None)
    check("exams KPI", k and k["value"] and _re.fullmatch(r"\d+/\d+ sections submitted", k.get("hint") or "")
          and k.get("href") == "/admin/exams", k)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Accountant"])
    tok = login(e, p)["tokens"]["accessToken"]
    kpis = data(call("GET", "/dashboard", token=tok, campus=a))["kpis"]
    check("no exams KPI for an Accountant", not any(x["id"] == "exams" for x in kpis), [x["id"] for x in kpis])


# ------------------------------------------------------------------ isolation / permissions

@group
def exams_isolation(hq):
    from attendance_e2e import qa_section, admit_on, today
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    bsec, bsdto = qa_section(hq, b, "G1")
    bs = admit_on(hq, b, bsec, today() - _dt.timedelta(days=60))
    beid, bpid = single_paper_exam(hq, b, bsdto, "MidTerm")
    enter_and_publish(hq, b, beid, bpid, bsec, [(bs, 77)])
    bsess = current_session(hq, b)
    g3 = class_by_code(hq, b, "G3")
    scheme = {"sessionId": None, "classId": g3["id"], "monthlyWeight": 0, "midTermWeight": 50, "finalWeight": 50, "otherWeight": 0,
              "mustPassFinal": False, "mainCampus": False}
    s, r = call("PUT", "/grading/schemes", scheme, token=hq, campus=b)
    bscheme = r["data"]["id"] if s == 200 else 0
    main_scheme = data(call("GET", "/grading/schemes", token=ca, campus=a))["effective"]

    attempts = [
        ("exam GET", call("GET", f"/exams/{beid}", token=ca, campus=a)[0]),
        ("exam PUT", call("PUT", f"/exams/{beid}", exam_body(session_id=bsess["id"]), token=ca, campus=a)[0]),
        ("exam status", call("POST", f"/exams/{beid}/status", {"status": "Closed"}, token=ca, campus=a)[0]),
        ("exam delete", call("DELETE", f"/exams/{beid}", token=ca, campus=a)[0]),
        ("paper update", call("PUT", f"/exams/{beid}/papers/{bpid}", {"totalMarks": 50, "passMarks": 20, "hasPractical": False,
                                                                      "practicalMarks": None, "paperDate": None, "isActive": True},
                              token=ca, campus=a)[0]),
        ("marks GET", call("GET", marks_url(bpid, bsec), token=ca, campus=a)[0]),
        ("marks PUT", save_marks(ca, a, bpid, bsec, [(bs, 1, None, "Present")])[0]),
        ("marks submit", call("POST", f"/marks/{bpid}/sections/{bsec}/submit", token=ca, campus=a)[0]),
        ("marks return", call("POST", f"/marks/{bpid}/sections/{bsec}/return", token=ca, campus=a)[0]),
        ("tabulation", tab(ca, a, beid, bsec)[0]),
        ("csv", download(f"/results/{beid}/sections/{bsec}/csv", ca, a)[0]),
        ("publish", call("POST", f"/results/{beid}/sections/{bsec}/publish", token=ca, campus=a)[0]),
        ("unpublish", call("POST", f"/results/{beid}/sections/{bsec}/unpublish", token=ca, campus=a)[0]),
        ("annual", call("GET", f"/results/annual?sessionId={bsess['id']}&sectionId={bsec}", token=ca, campus=a)[0]),
        ("exam cards", call("GET", f"/report-cards/exam/{beid}?sectionId={bsec}", token=ca, campus=a)[0]),
        ("student card", call("GET", f"/report-cards/exam/{beid}?studentId={bs}", token=ca, campus=a)[0]),
        ("annual cards", call("GET", f"/report-cards/annual?sessionId={bsess['id']}&sectionId={bsec}", token=ca, campus=a)[0]),
        ("student results", call("GET", f"/students/{bs}/results", token=ca, campus=a)[0]),
        ("remark", call("PUT", "/results/remarks", {"examId": beid, "sessionId": bsess["id"], "studentId": bs, "remark": "x"}, token=ca, campus=a)[0]),
        ("B scheme delete", call("DELETE", f"/grading/schemes/{bscheme}", token=ca, campus=a)[0]),
    ]
    check("every cross-campus exams action -> 404", all(c == 404 for _, c in attempts), attempts)
    s, _ = call("DELETE", f"/grading/schemes/{main_scheme['id']}", token=ca, campus=a)
    check("Main Campus scheme delete by a campus -> 403", main_scheme.get("campusId") is None and s == 403, (s, main_scheme))
    s, r = call("GET", "/exams", token=ca, campus=a)
    check("exam list shows no campus-B exams", s == 200 and not any(x["id"] == beid for x in r["data"]), s)
    s, r = call("GET", "/marks/my-papers", token=ca, campus=a)
    check("my papers show no campus-B sections", s == 200 and not any(x["sectionId"] == bsec for x in r["data"]), s)
    after = {x["studentId"]: x for x in tab(hq, b, beid, bsec)[1]["data"]["rows"]}
    check("campus-B result unchanged", after[bs]["percent"] == 77, after)

    # a Main Campus exam is shared; marks stay with each campus's sections
    meid, mdet = make_exam(hq, b, ("G1",), main=True)
    s, r = call("GET", "/exams", token=ca, campus=a)
    check("Main Campus exam visible to campus A", s == 200 and any(x["id"] == meid for x in r["data"]), s)
    asec, asdto = qa_section(ca, a, "G1")
    ast = admit_on(ca, a, asec, today() - _dt.timedelta(days=60))
    mpid = paper_of(mdet, asdto["className"], "Mathematics")["id"]
    s, _ = save_marks(ca, a, mpid, asec, [(ast, 66, None, "Present")])
    check("campus A enters marks on the Main Campus exam", s == 200, s)
    s, r = tab(hq, b, meid, bsec)
    check("campus A's marks never appear in campus B's tabulation", s == 200 and not any(x["studentId"] == ast for x in r["data"]["rows"]), (s, r))
    call("POST", f"/exams/{meid}/status", {"status": "Closed"}, token=hq, campus=b)
    call("POST", f"/exams/{beid}/status", {"status": "Closed"}, token=hq, campus=b)
    if bscheme:
        call("DELETE", f"/grading/schemes/{bscheme}", token=hq, campus=b)


def role_token(hq, campus, role):
    _, e, p = make_user(hq, campus_ids=[campus], role_names=[role])
    return login(e, p)["tokens"]["accessToken"]


@group
def exams_permissions(hq):
    from attendance_e2e import qa_section, admit_on, today
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sec, sdto = qa_section(ca, a, "G1")
    st = admit_on(ca, a, sec, today() - _dt.timedelta(days=60))
    eid, pid = single_paper_exam(ca, a, sdto, "MidTerm")
    enter_and_publish(ca, a, eid, pid, sec, [(st, 70)])
    pub = f"/results/{eid}/sections/{sec}"
    scale = data(call("GET", "/grading/scale", token=ca, campus=a))
    scale_body = {"name": "x", "bands": [{"grade": b["grade"], "minPercent": b["minPercent"], "isFail": b["isFail"]} for b in scale["bands"]],
                  "mainCampus": False}

    acc = role_token(hq, a, "Accountant")
    codes = [call("GET", "/exams", token=acc, campus=a)[0], call("GET", "/marks/my-papers", token=acc, campus=a)[0],
             call("GET", pub, token=acc, campus=a)[0], call("GET", f"/report-cards/exam/{eid}?sectionId={sec}", token=acc, campus=a)[0],
             call("GET", "/grading/scale", token=acc, campus=a)[0], save_marks(acc, a, pid, sec, [(st, 1, None, "Present")])[0]]
    check("Accountant 403 everywhere", all(c == 403 for c in codes), codes)

    clerk = role_token(hq, a, "Clerk")
    check("Clerk views results and cards", call("GET", pub, token=clerk, campus=a)[0] == 200
          and call("GET", f"/report-cards/exam/{eid}?sectionId={sec}", token=clerk, campus=a)[0] == 200)
    codes = [save_marks(clerk, a, pid, sec, [(st, 1, None, "Present")])[0], call("POST", pub + "/unpublish", token=clerk, campus=a)[0],
             call("POST", "/exams", exam_body(session_id=current_session(ca, a)["id"]), token=clerk, campus=a)[0]]
    check("Clerk cannot enter marks, publish or create exams", all(c == 403 for c in codes), codes)

    teacher = role_token(hq, a, "Teacher")
    codes = [call("POST", pub + "/unpublish", token=teacher, campus=a)[0], call("PUT", "/grading/scale", scale_body, token=teacher, campus=a)[0],
             call("POST", f"/exams/{eid}/status", {"status": "Closed"}, token=teacher, campus=a)[0]]
    check("Teacher cannot publish, change grading or exams", all(c == 403 for c in codes), codes)

    principal = role_token(hq, a, "Principal")
    codes = [call("GET", pub, token=principal, campus=a)[0], call("POST", pub + "/unpublish", token=principal, campus=a)[0],
             call("POST", pub + "/publish", token=principal, campus=a)[0], call("GET", "/grading/scale", token=principal, campus=a)[0]]
    check("Principal 200", all(c == 200 for c in codes), codes)
    call("POST", f"/exams/{eid}/status", {"status": "Closed"}, token=ca, campus=a)


# ------------------------------------------------------------------ final review fixes

@group
def exams_review_fixes(hq):
    from attendance_e2e import qa_section, admit_on, today, working_days_back, save, teacher_with_login, iso
    from onboarding_e2e import retire_staff
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)

    # moved_student_publishes_once: S sits Mathematics in A1, moves to A2, sits English there
    a1, sd1 = qa_section(ca, a, "G1")
    a2, _ = qa_section(ca, a, "G1")
    S, T = [admit_on(ca, a, a1, today() - _dt.timedelta(days=60)) for _ in range(2)]
    U = admit_on(ca, a, a2, today() - _dt.timedelta(days=60))
    eid, det = make_exam(ca, a, ("G1",))
    cls = next(c for c in det["classes"] if c["className"] == sd1["className"])
    P = {}
    for p in cls["papers"]:
        on = p["subjectName"] in ("Mathematics", "English")
        call("PUT", f"/exams/{eid}/papers/{p['id']}", {"totalMarks": 100, "passMarks": 33, "hasPractical": False, "practicalMarks": None,
                                                      "paperDate": None, "isActive": on}, token=ca, campus=a)
        if on:
            P[p["subjectName"]] = p["id"]
    save_marks(ca, a, P["Mathematics"], a1, [(S, 80, None, "Present"), (T, 70, None, "Present")])
    call("POST", f"/students/{S}/change-section", {"sectionId": a2, "rollNo": None, "reason": "QA move"}, token=ca, campus=a)
    save_marks(ca, a, P["English"], a1, [(T, 60, None, "Present")])
    save_marks(ca, a, P["Mathematics"], a2, [(U, 50, None, "Present")])
    s, r = save_marks(ca, a, P["English"], a2, [(S, 90, None, "Present"), (U, 55, None, "Present")])
    for pid in P.values():
        for sec in (a1, a2):
            call("POST", f"/marks/{pid}/sections/{sec}/submit", token=ca, campus=a)
    t1 = {x["studentId"]: x for x in tab(ca, a, eid, a1)[1]["data"]["rows"]}
    t2 = {x["studentId"]: x for x in tab(ca, a, eid, a2)[1]["data"]["rows"]}
    check("moved student is tabulated only in the new section, with both subjects",
          S not in t1 and S in t2 and t2[S]["obtained"] == 170 and t2[S]["passed"] is True, (list(t1), t2.get(S)))
    s1, r1 = call("POST", f"/results/{eid}/sections/{a1}/publish", token=ca, campus=a)
    s2, r2 = call("POST", f"/results/{eid}/sections/{a2}/publish", token=ca, campus=a)
    check("both sections publish", (s1, s2) == (200, 200), (s1, r1, s2, r2))

    # teacher_cannot_read_other_students_results
    t, tok = teacher_with_login(hq, ca, a)
    s, _ = call("GET", f"/students/{S}/results", token=tok, campus=a)
    check("unrelated teacher cannot read a student's results", s == 403, s)
    call("PUT", f"/sections/{a2}/class-teacher", {"staffId": t}, token=ca, campus=a)
    s, _ = call("GET", f"/students/{S}/results", token=tok, campus=a)
    check("class teacher reads own student's results", s == 200, s)
    call("PUT", f"/sections/{a2}/class-teacher", {"staffId": None}, token=ca, campus=a)

    # campus_exam_lists_only_its_sections: a campus-B exam on a shared class, viewed from all campuses
    bsec, _ = qa_section(hq, b, "G1")
    beid, _ = make_exam(hq, b, ("G1",))
    s, r = call("GET", f"/exams/{beid}", token=hq)
    secs = [x["sectionId"] for c in r["data"]["classes"] for x in c["sections"]] if s == 200 else []
    check("campus exam lists only its own campus's sections", s == 200 and bsec in secs and a1 not in secs and a2 not in secs, (s, secs))

    # card_attendance_until_exam_start: attendance after the exam's start date is not counted
    d = working_days_back(ca, a, 1, start_offset=1)[0]
    save(ca, a, a1, d, {T: "Absent"})
    ceid, cpid = single_paper_exam(ca, a, sd1, "Monthly", start=iso(d - _dt.timedelta(days=1)), end=iso(d + _dt.timedelta(days=1)))
    enter_and_publish(ca, a, ceid, cpid, a1, [(T, 50)])
    s, r = call("GET", f"/report-cards/exam/{ceid}?studentId={T}", token=ca, campus=a)
    check("card attendance stops at the exam start date", s == 200 and r["data"][0].get("attendancePct") is None, (s, r))
    for e in (eid, beid, ceid):
        call("POST", f"/exams/{e}/status", {"status": "Closed"}, token=hq, campus=None if e == beid else a)
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
