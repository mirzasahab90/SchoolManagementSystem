"""End-to-end checks for Students and Guardians (Part A).

Usage: python tests/e2e/students_e2e.py <group> [<group> ...]
Runs against the local dev API; every group cleans up what it creates.
"""
import sys

from sms_client import admin_token, call, check, cleanup, data, login, summary, wait_for_api
from academics_e2e import actions, campus_admin, demo_campuses, menu_keys, role_detail

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    me = data(call("GET", "/auth/me", token=hq))
    check("HQ top menu includes Students group", menu_keys(me) == ["dashboard", "campuses", "academics", "students-menu", "staff-menu", "attendance-menu", "exams-menu", "fees-menu", "settings"],
          menu_keys(me))
    grp = next((m for m in me["menu"] if m["key"] == "students-menu"), None)
    check("students group children", grp and [c["key"] for c in grp["children"]] == ["students", "guardians"], grp)
    full = {"view", "add", "edit", "delete"}
    teacher = role_detail(hq, "Teacher")
    check("Teacher can view students", actions(teacher, "students") == {"view"}, actions(teacher, "students"))
    clerk = role_detail(hq, "Clerk")
    check("Clerk students view+add, guardians view+edit",
          actions(clerk, "students") == {"view", "add"} and actions(clerk, "guardians") == {"view", "edit"},
          (actions(clerk, "students"), actions(clerk, "guardians")))
    ca = role_detail(hq, "CampusAdmin")
    check("CampusAdmin full on students and guardians",
          actions(ca, "students") == full and actions(ca, "guardians") == full,
          (actions(ca, "students"), actions(ca, "guardians")))


# ------------------------------------------------------------------ helpers

import datetime as _dt
import random as _random
import re as _re
import threading as _threading
from urllib.parse import quote as _quote

from sms_client import track


def rand_digits(n):
    return "".join(_random.choice("0123456789") for _ in range(n))


def new_guardian(**over):
    g = {"fullName": f"Qa Parent {rand_digits(4)}", "cnic": rand_digits(13), "phone": "0300" + rand_digits(7),
         "altPhone": None, "email": None, "occupation": None, "address": None}
    g.update(over)
    return g


def student_fields(**over):
    s = {"firstName": "Qa", "lastName": f"Student{rand_digits(4)}", "gender": "Male", "dateOfBirth": "2016-05-10",
         "bFormNo": None, "bloodGroup": None, "religion": None, "nationality": "Pakistani", "address": None, "phone": None,
         "admissionDate": _dt.date.today().isoformat(), "previousSchool": None, "notes": None}
    s.update(over)
    return s


def admit(token, campus, section_id, guardians=None, admission_no=None, roll_no=None, session_id=None, **student_over):
    body = {
        "student": student_fields(**student_over),
        "admissionNo": admission_no,
        "enrollment": {"sessionId": session_id, "sectionId": section_id, "rollNo": roll_no},
        "guardians": guardians if guardians is not None else [
            {"guardianId": None, "newGuardian": new_guardian(), "relation": "Father", "isPrimary": True, "isEmergencyContact": True}],
    }
    s, r = call("POST", "/students", body, token=token, campus=campus)
    if s == 200:
        track("students", r["data"]["student"]["id"])
    return s, r


def section_in(token, campus, class_code, name):
    rows = data(call("GET", "/sections", token=token, campus=campus)) or []
    return next(x["id"] for x in rows if x["classCode"] == class_code and x["name"] == name)


def campus_code(token, campus_id):
    return data(call("GET", f"/campuses/{campus_id}", token=token))["code"]


# ------------------------------------------------------------------ students

@group
def students(hq):
    a, _ = demo_campuses(hq)
    code = campus_code(hq, a)
    year = _dt.date.today().year
    g4a = section_in(hq, a, "G4", "A")

    s, r = admit(hq, a, g4a)
    ok = check("admission creates student, guardian, enrollment, history", s == 200
               and _re.fullmatch(rf"{code}-{year}-\d{{4}}", r["data"]["student"]["admissionNo"])
               and len(r["data"]["guardians"]) == 1 and r["data"]["guardians"][0]["isPrimary"]
               and r["data"]["enrollment"]["sectionId"] == g4a and r["data"]["enrollment"]["status"] == "Active"
               and r["data"]["enrollment"]["rollNo"] >= 1
               and [h["action"] for h in r["data"]["history"]] == ["Admitted"], (s, r))
    if not ok:
        return
    first = r["data"]
    n1 = int(first["student"]["admissionNo"].rsplit("-", 1)[1])

    s, r = admit(hq, a, g4a)
    n2 = int(r["data"]["student"]["admissionNo"].rsplit("-", 1)[1]) if s == 200 else None
    check("admission numbers increment", n2 == n1 + 1, (n1, n2))

    typed = f"{code}-{year}-{n2 + 1:04d}"
    s, r = admit(hq, a, g4a, admission_no=typed)
    check("typed admission number accepted", s == 200 and r["data"]["student"]["admissionNo"] == typed, (s, r))
    s, r = admit(hq, a, g4a)
    n4 = int(r["data"]["student"]["admissionNo"].rsplit("-", 1)[1]) if s == 200 else None
    check("generator skips typed numbers", n4 == n2 + 2, (n2, n4))

    bform = rand_digits(13)
    admit(hq, a, g4a, bFormNo=bform)
    s, r = admit(hq, a, g4a, bFormNo=bform)
    check("duplicate B-Form -> 409", s == 409, (s, r))

    from academics_e2e import create_section, create_session, session_body
    s, r = create_session(hq, session_body("QA-STU", "2091-04-01", "2092-03-31", "Active"))
    if s == 200:
        sid = r["data"]["id"]
        s, r = create_section(hq, data(call("GET", "/classes", token=hq))[3]["id"], "QZ", academicSessionId=sid, campusId=a)
        if s == 200:
            closed_sec = r["data"]["id"]
            call("PUT", f"/academic-sessions/{sid}", session_body("QA-STU", "2091-04-01", "2092-03-31", "Closed"), token=hq)
            s, r = admit(hq, a, closed_sec)
            check("admission into a closed session -> 400", s == 400 and "closed" in str(r).lower(), (s, r))
            call("PUT", f"/academic-sessions/{sid}", session_body("QA-STU", "2091-04-01", "2092-03-31", "Planned"), token=hq)

    g4b = section_in(hq, a, "G4", "B")
    sec = next(x for x in data(call("GET", "/sections", token=hq, campus=a)) if x["id"] == g4b)
    body = {k: sec.get(k) for k in ("classId", "academicSessionId", "name", "shift", "capacity", "room")}
    call("PUT", f"/sections/{g4b}", dict(body, isActive=False), token=hq, campus=a)
    s, r = admit(hq, a, g4b)
    check("admission into an inactive section -> 400", s == 400, (s, r))
    call("PUT", f"/sections/{g4b}", dict(body, isActive=True), token=hq, campus=a)

    two = [{"guardianId": None, "newGuardian": new_guardian(), "relation": "Father", "isPrimary": False, "isEmergencyContact": False},
           {"guardianId": None, "newGuardian": new_guardian(), "relation": "Mother", "isPrimary": False, "isEmergencyContact": False}]
    s, r = admit(hq, a, g4a, guardians=two)
    check("no primary guardian -> 400", s == 400, (s, r))

    existing_cnic = first["guardians"][0]["cnic"]
    s, r = admit(hq, a, g4a, guardians=[{"guardianId": None, "newGuardian": new_guardian(cnic=existing_cnic),
                                         "relation": "Father", "isPrimary": True, "isEmergencyContact": False}])
    check("new guardian with existing CNIC -> 409", s == 409 and "link them instead" in str(r), (s, r))

    adm = first["student"]["admissionNo"]
    last = first["student"]["lastName"]
    phone = first["guardians"][0]["phone"]
    for label, q in (("admission number", adm), ("last name", last), ("guardian phone", phone)):
        s, r = call("GET", f"/students?q={q}", token=hq, campus=a)
        check(f"search by {label}", s == 200 and any(x["id"] == first["student"]["id"] for x in r["data"]["items"]), (s, r))

    dashed = "0300-" + rand_digits(7)
    s, r = admit(hq, a, g4a, guardians=[{"guardianId": None, "newGuardian": new_guardian(phone=dashed),
                                         "relation": "Mother", "isPrimary": True, "isEmergencyContact": False}])
    if s == 200:
        sid2 = r["data"]["student"]["id"]
        s, r = call("GET", f"/students?q={dashed.replace('-', '')}", token=hq, campus=a)
        check("search phone ignores formatting", any(x["id"] == sid2 for x in r["data"]["items"]), r)

    st = first["student"]
    upd = {k: st.get(k) for k in ("firstName", "lastName", "gender", "dateOfBirth", "bFormNo", "bloodGroup", "religion",
                                  "nationality", "address", "phone", "admissionDate", "previousSchool", "notes")}
    s, r = call("PUT", f"/students/{st['id']}", dict(upd, address="House 1, Street 2"), token=hq, campus=a)
    check("update profile", s == 200 and r["data"]["student"]["address"] == "House 1, Street 2", (s, r))


@group
def admission_concurrency(hq):
    a, _ = demo_campuses(hq)
    g5a = section_in(hq, a, "G5", "A")
    results = []

    def one():
        results.append(admit(hq, a, g5a))

    threads = [_threading.Thread(target=one) for _ in range(5)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    nos = [r["data"]["student"]["admissionNo"] for s, r in results if s == 200]
    check("5 parallel admissions all succeed", len(nos) == 5, [s for s, _ in results])
    check("parallel admissions get distinct numbers", len(set(nos)) == len(nos), nos)


# ------------------------------------------------------------------ guardians

def link_existing(guardian_id, relation="Father", primary=True, proof=None):
    return {"guardianId": guardian_id, "newGuardian": proof, "relation": relation, "isPrimary": primary, "isEmergencyContact": False}


@group
def guardians(hq):
    a, b = demo_campuses(hq)
    g4a = section_in(hq, a, "G4", "A")
    g4b_campus_b = section_in(hq, b, "G4", "A")
    _, ca = campus_admin(hq, a)

    s, r = admit(hq, a, g4a)
    s1 = r["data"]
    gid = s1["guardians"][0]["guardianId"]
    cnic = s1["guardians"][0]["cnic"]
    s, r = admit(hq, a, g4a, guardians=[link_existing(gid)])
    s2 = r["data"] if s == 200 else None
    prof1 = data(call("GET", f"/students/{s1['student']['id']}", token=hq, campus=a))
    check("linking an existing guardian makes siblings", s2 and any(x.get("studentId") == s2["student"]["id"] for x in prof1["siblings"]),
          prof1 and prof1["siblings"])

    s, r = admit(hq, b, g4b_campus_b, guardians=[link_existing(gid)])
    s3 = r["data"] if s == 200 else None
    if s3:
        p = data(call("GET", f"/students/{s1['student']['id']}", token=ca, campus=a))
        sib = next((x for x in p["siblings"] if x["fullName"] == s3["student"]["fullName"]), None)
        check("cross-campus sibling hides the student id", sib and sib.get("studentId") is None and sib["campusCode"] == campus_code(hq, b), sib)
        gd = data(call("GET", f"/guardians/{gid}", token=ca, campus=a))
        child = gd and next((x for x in gd["children"] if x["fullName"] == s3["student"]["fullName"]), None)
        check("guardian page hides other-campus child id", child and child.get("studentId") is None, gd)
        s, _ = call("GET", f"/students/{s3['student']['id']}", token=ca, campus=a)
        check("other-campus student profile -> 404", s == 404, s)

    s, r = call("GET", f"/guardians/lookup?cnic={cnic}", token=ca, campus=a)
    rows = r["data"] if s == 200 else []
    check("lookup returns only id, name, children count", len(rows) == 1 and set(rows[0]) == {"id", "fullName", "childrenCount"}, (s, r))
    s, r = call("GET", "/guardians/lookup", token=ca, campus=a)
    check("lookup without cnic or phone -> 400", s == 400, (s, r))

    sid = s1["student"]["id"]
    s, r = call("POST", f"/students/{sid}/guardians",
                {"guardianId": None, "newGuardian": new_guardian(), "relation": "Mother", "isPrimary": True, "isEmergencyContact": False},
                token=ca, campus=a)
    prim = [g for g in r["data"]["guardians"] if g["isPrimary"]] if s == 200 else []
    check("new primary demotes the old one", s == 200 and len(prim) == 1 and prim[0]["relation"] == "Mother", (s, r))
    mother = prim[0]["guardianId"] if prim else None
    if mother:
        s, r = call("DELETE", f"/students/{sid}/guardians/{mother}", token=ca, campus=a)
        check("cannot remove the primary guardian", s == 400 and "Choose another primary guardian first." in str(r), (s, r))
        s, r = call("DELETE", f"/students/{sid}/guardians/{gid}", token=ca, campus=a)
        check("can remove a non-primary guardian", s == 200 and len(r["data"]["guardians"]) == 1, (s, r))
    s, r = call("POST", f"/students/{sid}/guardians", link_existing(mother, relation="Mother", primary=False), token=ca, campus=a)
    check("linking an already linked guardian -> 409", s == 409, (s, r))

    # a guardian whose only children are on campus B is invisible to campus A
    s, r = admit(hq, b, g4b_campus_b)
    if s == 200:
        hidden = r["data"]["guardians"][0]["guardianId"]
        s, _ = call("GET", f"/guardians/{hidden}", token=ca, campus=a)
        check("guardian without visible students -> 404", s == 404, s)
        listed = [x["id"] for x in data(call("GET", "/guardians?pageSize=100&q=" + _quote(r["data"]["guardians"][0]["fullName"]), token=ca, campus=a))["items"]]
        check("guardian without visible students not listed", hidden not in listed, listed)
        s, _ = admit(ca, a, g4a, guardians=[link_existing(hidden)])
        check("linking an unseen guardian without proof -> 404", s == 404, s)
        s, _ = admit(ca, a, g4a, guardians=[link_existing(hidden, proof={"fullName": "x", "phone": r["data"]["guardians"][0]["phone"]})])
        check("linking an unseen guardian with matching phone works", s == 200, s)


# ------------------------------------------------------------------ enrollment

@group
def enrollment(hq):
    a, b = demo_campuses(hq)
    g3a, g3b = section_in(hq, a, "G3", "A"), section_in(hq, a, "G3", "B")
    other_campus = section_in(hq, b, "G3", "A")
    _, ca = campus_admin(hq, a)

    s, r = admit(ca, a, g3a)
    sid = r["data"]["student"]["id"]
    s, r = call("POST", f"/students/{sid}/change-section", {"sectionId": g3b, "rollNo": None, "reason": "Balance sections"}, token=ca, campus=a)
    hist = r["data"]["history"] if s == 200 else []
    check("section change moves the student and writes history", s == 200 and r["data"]["enrollment"]["sectionId"] == g3b
          and hist[-1]["action"] == "SectionChanged" and hist[-1]["fromSection"].endswith(" A")
          and hist[-1]["toSection"].endswith(" B"), (s, r))

    s2, r2 = admit(ca, a, g3a)
    taken = r2["data"]["enrollment"]["rollNo"] if s2 == 200 else None
    s, r = call("POST", f"/students/{sid}/change-section", {"sectionId": g3a, "rollNo": taken, "reason": None}, token=ca, campus=a)
    check("section change into a taken roll number -> 409", s == 409, (s, r))
    s, r = call("POST", f"/students/{sid}/change-section", {"sectionId": other_campus, "rollNo": None, "reason": None}, token=hq, campus=a)
    check("section change to another campus's section -> 400", s == 400, (s, r))

    today = _dt.date.today().isoformat()
    s, r = call("POST", f"/students/{sid}/leave", {"leftOn": "2000-01-01", "reason": "Moved"}, token=ca, campus=a)
    check("leaving before the admission date -> 400", s == 400, (s, r))
    s, r = call("POST", f"/students/{sid}/leave", {"leftOn": today, "reason": "Moved city"}, token=ca, campus=a)
    check("leave marks the student Left", s == 200 and r["data"]["student"]["status"] == "Left"
          and r["data"]["enrollment"]["status"] == "Left", (s, r))
    s, r = call("POST", f"/students/{sid}/change-section", {"sectionId": g3a, "rollNo": None, "reason": None}, token=ca, campus=a)
    check("section change of a Left student -> 400", s == 400 and "Rejoin the student first." in str(r), (s, r))
    s, r = call("POST", f"/students/{sid}/rejoin", {"sectionId": None, "rollNo": None}, token=ca, campus=a)
    check("rejoin returns to the same section", s == 200 and r["data"]["student"]["status"] == "Active"
          and r["data"]["enrollment"]["sectionId"] == g3b and r["data"]["enrollment"]["status"] == "Active"
          and [h["action"] for h in r["data"]["history"]][-2:] == ["Left", "Rejoined"], (s, r))

    # the old section becomes unusable while the student is away
    call("POST", f"/students/{sid}/leave", {"leftOn": today, "reason": "Again"}, token=ca, campus=a)
    sec = next(x for x in data(call("GET", "/sections", token=hq, campus=a)) if x["id"] == g3b)
    body = {k: sec.get(k) for k in ("classId", "academicSessionId", "name", "shift", "capacity", "room")}
    call("PUT", f"/sections/{g3b}", dict(body, isActive=False), token=hq, campus=a)
    try:
        s, r = call("POST", f"/students/{sid}/rejoin", {"sectionId": None, "rollNo": None}, token=ca, campus=a)
        check("rejoin without a usable old section -> 400 Pick a section.", s == 400 and "Pick a section." in str(r), (s, r))
        s, r = call("POST", f"/students/{sid}/rejoin", {"sectionId": g3a, "rollNo": None}, token=ca, campus=a)
        check("rejoin into another section works", s == 200 and r["data"]["enrollment"]["sectionId"] == g3a, (s, r))
    finally:
        call("PUT", f"/sections/{g3b}", dict(body, isActive=True), token=hq, campus=a)


# ------------------------------------------------------------------ documents

import base64 as _b64

from sms_client import download, upload

PNG = _b64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")
PDF = b"%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"


@group
def documents(hq):
    a, _ = demo_campuses(hq)
    g2a = section_in(hq, a, "G2", "A")
    _, ca = campus_admin(hq, a)
    s, r = admit(ca, a, g2a)
    sid = r["data"]["student"]["id"]

    s, r = upload(f"/students/{sid}/photo", {}, "file", "me.png", PNG, "image/png", ca, a)
    prof = data(call("GET", f"/students/{sid}", token=ca, campus=a))
    st, body, headers = download(f"/students/{sid}/photo", ca, a)
    check("photo upload and fetch", s == 200 and prof["student"]["hasPhoto"] and st == 200 and body == PNG
          and headers.get("Content-Type", "").startswith("image/png"), (s, r, st))

    s, r = upload(f"/students/{sid}/documents", {"type": "BForm"}, "file", "b-form scan.pdf", PDF, "application/pdf", ca, a)
    doc = r["data"] if s == 200 else None
    docs = data(call("GET", f"/students/{sid}", token=ca, campus=a))["documents"]
    st, body, headers = download(f"/students/{sid}/documents/{doc['id']}", ca, a) if doc else (0, b"", {})
    check("document upload, list and download", doc and any(d["id"] == doc["id"] for d in docs) and st == 200 and body == PDF
          and "b-form scan.pdf" in headers.get("Content-Disposition", ""), (s, r, st, headers.get("Content-Disposition")))
    if doc:
        s, _ = call("DELETE", f"/students/{sid}/documents/{doc['id']}", token=ca, campus=a)
        docs = data(call("GET", f"/students/{sid}", token=ca, campus=a))["documents"]
        check("document delete", s == 200 and all(d["id"] != doc["id"] for d in docs), s)

    s, r = upload(f"/students/{sid}/documents", {"type": "Other"}, "file", "x.pdf", b"MZ\x90\x00" + b"\x00" * 64, "application/pdf", ca, a)
    check("renamed executable refused", s == 400, (s, r))
    s, r = upload(f"/students/{sid}/photo", {}, "file", "doc.pdf", PDF, "application/pdf", ca, a)
    check("PDF refused as a photo", s == 400, (s, r))
    s, r = upload(f"/students/{sid}/photo", {}, "file", "big.png", PNG + b"\x00" * (2_097_153 - len(PNG)), "image/png", ca, a)
    check("oversize photo refused", s == 400, (s, r))

    s, r = upload(f"/students/{sid}/documents", {"type": "Other"}, "file", "keep.pdf", PDF, "application/pdf", ca, a)
    kept = r["data"]["id"] if s == 200 else None
    s, _ = call("DELETE", f"/students/{sid}", token=ca, campus=a)
    p, _, _ = download(f"/students/{sid}/photo", ca, a)
    d, _, _ = download(f"/students/{sid}/documents/{kept}", ca, a)
    check("deleted student's files -> 404", s == 200 and p == 404 and d == 404, (s, p, d))


# ------------------------------------------------------------------ isolation & dashboard

@group
def student_isolation(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    s, r = admit(hq, b, section_in(hq, b, "G2", "A"))
    st = r["data"]["student"]
    bid, gid = st["id"], r["data"]["guardians"][0]["guardianId"]
    s, r = upload(f"/students/{bid}/documents", {"type": "Other"}, "file", "b.pdf", PDF, "application/pdf", hq, b)
    doc = r["data"]["id"]
    upload(f"/students/{bid}/photo", {}, "file", "b.png", PNG, "image/png", hq, b)
    g2a = section_in(hq, a, "G2", "A")
    today = _dt.date.today().isoformat()
    body = {k: st.get(k) for k in ("firstName", "lastName", "gender", "dateOfBirth", "bFormNo", "bloodGroup", "religion",
                                  "nationality", "address", "phone", "admissionDate", "previousSchool", "notes")}

    attempts = [
        ("GET profile", call("GET", f"/students/{bid}", token=ca, campus=a)[0]),
        ("PUT profile", call("PUT", f"/students/{bid}", dict(body, address="hacked"), token=ca, campus=a)[0]),
        ("change-section", call("POST", f"/students/{bid}/change-section", {"sectionId": g2a, "rollNo": None, "reason": None}, token=ca, campus=a)[0]),
        ("leave", call("POST", f"/students/{bid}/leave", {"leftOn": today, "reason": "x"}, token=ca, campus=a)[0]),
        ("rejoin", call("POST", f"/students/{bid}/rejoin", {"sectionId": None, "rollNo": None}, token=ca, campus=a)[0]),
        ("add guardian", call("POST", f"/students/{bid}/guardians", {"guardianId": None, "newGuardian": new_guardian(),
                                                                      "relation": "Other", "isPrimary": False, "isEmergencyContact": False}, token=ca, campus=a)[0]),
        ("photo", download(f"/students/{bid}/photo", ca, a)[0]),
        ("document", download(f"/students/{bid}/documents/{doc}", ca, a)[0]),
        ("guardian", call("GET", f"/guardians/{gid}", token=ca, campus=a)[0]),
        ("DELETE", call("DELETE", f"/students/{bid}", token=ca, campus=a)[0]),
    ]
    check("every cross-campus student action -> 404", all(code == 404 for _, code in attempts), attempts)
    after = data(call("GET", f"/students/{bid}", token=hq, campus=b))
    check("campus-B student unchanged", after["student"].get("address") == st.get("address") and after["student"]["status"] == "Active"
          and len(after["guardians"]) == 1, after["student"])
    s, r = call("GET", f"/students?q={_quote(st['admissionNo'])}", token=ca, campus=a)
    check("campus-A search cannot find campus-B student", s == 200 and r["data"]["total"] == 0, r)
    s, r = call("GET", f"/students?q={_quote(st['admissionNo'])}", token=hq)
    check("HQ on All campuses finds it with campus code", s == 200 and r["data"]["items"][0]["campusCode"] == campus_code(hq, b), r)


@group
def dashboard_students(hq):
    a, _ = demo_campuses(hq)
    admit(hq, a, section_in(hq, a, "G1", "A"))
    for campus in (None, a):
        kpis = data(call("GET", "/dashboard", token=hq, campus=campus))["kpis"]
        k = next((x for x in kpis if x["id"] == "students"), None)
        total = data(call("GET", "/students?status=Active&pageSize=1", token=hq, campus=campus))["total"]
        check(f"students KPI ({'all' if campus is None else 'campus'})", k and k["value"] == str(total)
              and _re.fullmatch(r"\d+ admitted this month", k.get("hint") or "") and k.get("href") == "/admin/students", (k, total))


# ------------------------------------------------------------------ review-fixes (final review findings)

@group
def review_fixes(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    g5a, g5b = section_in(hq, a, "G5", "A"), section_in(hq, a, "G5", "B")

    # 1. other-campus sibling shows only name and campus code
    s, r = admit(hq, a, g5a)
    gid = r["data"]["guardians"][0]["guardianId"]
    s, r = admit(hq, b, section_in(hq, b, "G5", "A"), guardians=[link_existing(gid)])
    p = data(call("GET", f"/students/{_created_first()}", token=ca, campus=a))
    sib = next((x for x in p["siblings"] if x.get("studentId") is None), None)
    check("other-campus sibling hides class and section", sib and not sib.get("className") and not sib.get("sectionName"), sib)

    # 2. racing duplicates return 409, never 500
    code = campus_code(hq, a)
    typed = f"{code}-2099-{rand_digits(4)}"
    results = []
    threads = [_threading.Thread(target=lambda: results.append(admit(hq, a, g5a, admission_no=typed)[0])) for _ in range(2)]
    [t.start() for t in threads]
    [t.join() for t in threads]
    check("same typed admission number in parallel -> 200 + 409 (no 500)", sorted(results) == [200, 409], results)

    # 3. rejoin when the old roll was given to someone else uses the next free roll
    s, r = admit(ca, a, g5b)
    sid, roll = r["data"]["student"]["id"], r["data"]["enrollment"]["rollNo"]
    call("POST", f"/students/{sid}/leave", {"leftOn": _dt.date.today().isoformat(), "reason": "away"}, token=ca, campus=a)
    s, r = admit(ca, a, g5b, roll_no=roll)
    s, r = call("POST", f"/students/{sid}/rejoin", {"sectionId": None, "rollNo": None}, token=ca, campus=a)
    check("rejoin picks a free roll when the old one is taken", s == 200 and r["data"]["enrollment"]["rollNo"] != roll, (s, r))

    # 6. phone stored with +92 and dashes is found by its digits
    plus = "+92-300-" + rand_digits(7)
    s, r = admit(hq, a, g5a, guardians=[{"guardianId": None, "newGuardian": new_guardian(phone=plus, cnic=None),
                                         "relation": "Father", "isPrimary": True, "isEmergencyContact": False}])
    s, r = call("GET", f"/guardians/lookup?phone={plus.replace('+', '').replace('-', '')}", token=ca, campus=a)
    check("lookup finds +92 formatted phone", s == 200 and len(r["data"]) == 1, (s, r))

    # 7. an existing linked guardian can be made primary
    s, r = admit(ca, a, g5a)
    sid = r["data"]["student"]["id"]
    s, r = call("POST", f"/students/{sid}/guardians", {"guardianId": None, "newGuardian": new_guardian(), "relation": "Mother",
                                                         "isPrimary": False, "isEmergencyContact": False}, token=ca, campus=a)
    mother = next(g["guardianId"] for g in r["data"]["guardians"] if g["relation"] == "Mother")
    s, r = call("PATCH", f"/students/{sid}/guardians/{mother}", {"isPrimary": True, "isEmergencyContact": True}, token=ca, campus=a)
    prim = [g for g in r["data"]["guardians"] if g["isPrimary"]] if s == 200 else []
    check("make an existing guardian primary", s == 200 and len(prim) == 1 and prim[0]["guardianId"] == mother, (s, r))


def _created_first():
    from sms_client import _created
    return _created["students"][-2]


# ------------------------------------------------------------------ role-access (found in role walkthrough)

@group
def role_access(hq):
    from sms_client import make_user
    a, _ = demo_campuses(hq)
    _, ce, cp = make_user(hq, campus_ids=[a], role_names=["Clerk"])
    clerk = login(ce, cp)["tokens"]["accessToken"]
    _, te, tp = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    teacher = login(te, tp)["tokens"]["accessToken"]

    for name, tok, path in [("clerk sessions", clerk, "/academic-sessions"), ("clerk classes", clerk, "/classes"),
                            ("clerk subjects", clerk, "/subjects"), ("clerk sections", clerk, "/sections"),
                            ("clerk assignable roles", clerk, "/roles?assignable=true"), ("clerk campuses", clerk, "/campuses"),
                            ("teacher sessions", teacher, "/academic-sessions")]:
        s, r = call("GET", path, token=tok, campus=a)
        check(f"{name} list readable for its pages", s == 200, (s, r))

    s, _ = call("POST", "/classes", {"code": "QX1", "name": "x", "stage": "Middle", "displayOrder": 50, "isActive": True}, token=clerk, campus=a)
    check("clerk still cannot add a class", s == 403, s)
    s, _ = call("GET", "/roles", token=teacher, campus=a)
    check("teacher still cannot list roles", s == 403, s)

    actions = [q["id"] for q in data(call("GET", "/dashboard", token=teacher, campus=a))["quickActions"]]
    check("teacher dashboard shows no admin shortcuts", actions == [], actions)
    actions = [q["id"] for q in data(call("GET", "/dashboard", token=clerk, campus=a))["quickActions"]]
    check("clerk dashboard shortcuts match permissions", "roles" not in actions and "add-user" in actions, actions)


# ------------------------------------------------------------------ list-filters (sessionId, admitted range, sort)

@group
def list_filters(hq):
    from academics_e2e import create_section, create_session, session_body
    a, b = demo_campuses(hq)
    tag = "Lf" + rand_digits(5)
    year = _random.randint(2200, 2899)
    s, r = create_session(hq, session_body(f"QA-LF{rand_digits(3)}", f"{year}-04-01", f"{year + 1}-03-31", "Planned"))
    if not check("list-filters: setup session", s == 200, (s, r)):
        return
    sess = r["data"]["id"]
    classes = data(call("GET", "/classes", token=hq))
    s, r = create_section(hq, classes[3]["id"], "QL", academicSessionId=sess, campusId=a)
    if not check("list-filters: setup section", s == 200, (s, r)):
        return
    qsec = r["data"]["id"]

    ids = {}
    for letter, date, roll, sec in (("A", "2020-01-10", 3, qsec), ("B", "2020-02-10", 1, qsec), ("C", "2020-03-10", 2, qsec),
                                    ("D", "2020-02-15", None, section_in(hq, a, "G4", "A"))):
        s, r = admit(hq, a, sec, roll_no=roll, lastName=tag + letter, admissionDate=date)
        if not check(f"list-filters: admit {letter}", s == 200, (s, r)):
            return
        ids[letter] = r["data"]["student"]["id"]
    by_id = {v: k for k, v in ids.items()}

    def get(token=hq, campus=a, **params):
        qs = "&".join(f"{k}={_quote(str(v))}" for k, v in dict(q=tag, **params).items())
        s, r = call("GET", f"/students?{qs}", token=token, campus=campus)
        return s, (r["data"] if s == 200 else r)

    def letters(page):
        return [by_id.get(x["id"], "?") for x in page["items"]]

    s, p = get()
    check("default: all four, name order", s == 200 and p["total"] == 4 and letters(p) == ["A", "B", "C", "D"], (s, p))
    s, p = get(sessionId=sess)
    check("sessionId narrows to that session's enrollments", s == 200 and p["total"] == 3 and sorted(letters(p)) == ["A", "B", "C"], (s, p))
    s, p = get(admittedFrom="2020-02-10", admittedTo="2020-02-15")
    check("admitted range is inclusive on both ends", s == 200 and p["total"] == 2 and letters(p) == ["B", "D"], (s, p))
    s, p = get(admittedFrom="2020-03-01")
    check("admittedFrom alone", s == 200 and letters(p) == ["C"], (s, p))
    s, p = get(admittedTo="2020-01-10")
    check("admittedTo alone", s == 200 and letters(p) == ["A"], (s, p))

    s, p = get(sort="admitted")
    check("sort admitted asc", s == 200 and letters(p) == ["A", "B", "D", "C"], (s, p))
    s, p = get(sort="admitted", dir="desc")
    check("sort admitted desc", s == 200 and letters(p) == ["C", "D", "B", "A"], (s, p))
    s, p = get(sort="name", dir="desc")
    check("sort name desc", s == 200 and letters(p) == ["D", "C", "B", "A"], (s, p))
    s, p = get(sort="admissionNo")
    nos = [x["admissionNo"] for x in p["items"]] if s == 200 else None
    check("sort admissionNo asc", nos == sorted(nos or []) and len(nos) == 4, nos)
    s, p = get(sort="admissionNo", dir="desc")
    check("sort admissionNo desc", s == 200 and [x["admissionNo"] for x in p["items"]] == sorted(nos or [], reverse=True), p)
    s, p = get(sessionId=sess, sort="roll")
    check("sort roll asc", s == 200 and letters(p) == ["B", "C", "A"], (s, p))
    s, p = get(sessionId=sess, sort="roll", dir="desc")
    check("sort roll desc", s == 200 and letters(p) == ["A", "C", "B"], (s, p))
    order = {c["name"]: c["displayOrder"] for c in classes}
    s, p = get(sort="class")
    key = lambda x: (order.get(x["className"], 0), x["sectionName"] or "", x["rollNo"] or 0)
    check("sort class asc", s == 200 and [key(x) for x in p["items"]] == sorted(key(x) for x in p["items"]), p)
    s, p = get(sort="class", dir="desc")
    check("sort class desc", s == 200 and [key(x) for x in p["items"]] == sorted((key(x) for x in p["items"]), reverse=True), p)
    s, p = get(sort="bogus", dir="desc")
    check("unknown sort is ignored (default order)", s == 200 and letters(p) == ["A", "B", "C", "D"], (s, p))

    s, p = get(sort="name", pageSize=2, page=2)
    check("paging after sort: page 2 of 2", s == 200 and p["total"] == 4 and letters(p) == ["C", "D"], (s, p))
    s, p = get(sort="name", dir="desc", pageSize=3, page=1)
    check("paging after sort desc: first page", s == 200 and p["total"] == 4 and letters(p) == ["D", "C", "B"], (s, p))

    _, ca_a = campus_admin(hq, a)
    _, ca_b = campus_admin(hq, b)
    s, p = get(token=ca_a, sessionId=sess)
    check("campus-A admin sees its session filter rows", s == 200 and p["total"] == 3, (s, p))
    s, p = get(token=ca_b, campus=b, sessionId=sess)
    check("campus-B admin cannot see campus-A rows via sessionId", s == 200 and p["total"] == 0, (s, p))
    s, p = get(token=ca_b, campus=b, admittedFrom="2020-01-01", admittedTo="2020-12-31", sort="admitted")
    check("campus-B admin cannot see campus-A rows via admitted range", s == 200 and p["total"] == 0, (s, p))


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
