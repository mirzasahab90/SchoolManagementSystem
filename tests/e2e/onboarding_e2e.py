"""End-to-end checks for Staff Onboarding (Part 2).

Usage: python tests/e2e/onboarding_e2e.py <group> [<group> ...]
Runs against the local dev API; every group cleans up what it creates where the API allows it.
Staff that cannot be deleted are resigned at the end so their CNIC is free again.
"""
import datetime as _dt
import re as _re
import sys
import threading as _threading

from sms_client import (admin_token, call, check, cleanup, data, download, login, make_user, role_id, summary, temp_password,
                        track, upload, wait_for_api, _created)
from academics_e2e import campus_admin, create_role, demo_campuses, full_matrix
from hiring_e2e import (PDF, campus_code, hire_body, interviewer, make_candidate, make_vacancy, move, rand_digits,
                        retire_candidates, to_offer_accepted, vacancy_body)

GROUPS = {}
PNG = bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) + b"\x00" * 64


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def today(days=0):
    return (_dt.date.today() + _dt.timedelta(days=days)).isoformat()


def staff_body(**over):
    body = {"firstName": "Qa", "lastName": "Staff " + rand_digits(4), "cnic": rand_digits(13), "phone": "0301-" + rand_digits(7),
            "email": None, "gender": "Female", "dateOfBirth": "1990-05-01", "address": "QA street",
            "emergencyName": "Qa Kin", "emergencyRelation": "Brother", "emergencyPhone": "0302-" + rand_digits(7),
            "category": "NonTeaching", "designation": "Clerk", "employmentType": "Permanent", "joiningDate": today(-30),
            "basicSalary": 45000, "qualification": "BA", "experienceYears": 2, "previousEmployer": "QA Co",
            "bankName": "QA Bank", "bankAccount": "PK36SCBL0000001123456702"}
    body.update(over)
    return body


def teacher_body(**over):
    return staff_body(category="Teaching", designation="Teacher", **over)


def make_staff(token, campus, **over):
    s, r = call("POST", "/staff", staff_body(**over), token=token, campus=campus)
    if s != 200:
        raise AssertionError(f"make_staff: {s} {r}")
    return track("staff", r["data"]["staff"]["id"]), r["data"]


def retire_staff(hq):
    """Resign tracked staff that cannot be deleted (login, assignments, hired) so cleanup is quiet and CNICs free up."""
    for sid in list(_created["staff"]):
        s, _ = call("DELETE", f"/staff/{sid}", token=hq)
        if s == 200:
            _created["staff"].remove(sid)
            continue
        call("POST", f"/staff/{sid}/status", {"status": "Resigned", "date": today(), "reason": "qa cleanup"}, token=hq)
        _created["staff"].remove(sid)


# ------------------------------------------------------------------ direct

@group
def direct(hq):
    a, _ = demo_campuses(hq)
    code = campus_code(hq, a)
    _, ca = campus_admin(hq, a)
    body = staff_body()
    s, r = call("POST", "/staff", body, token=ca, campus=a)
    ok = check("direct onboarding", s == 200 and _re.fullmatch(rf"{_re.escape(code)}-EMP-\d{{4}}", r["data"]["staff"]["employeeNo"])
               and r["data"]["staff"]["status"] == "Onboarding" and not r["data"]["staff"].get("sourceCandidateId"), (s, r))
    if not ok:
        return
    sid = track("staff", r["data"]["staff"]["id"])
    p = r["data"]
    check("bank_masked: profile shows ****last4", p["personal"].get("bankAccountMasked") == "****" + body["bankAccount"][-4:], p["personal"])
    s, r = call("GET", f"/staff/{sid}", token=ca, campus=a)
    check("bank_masked: full number never in profile", s == 200 and body["bankAccount"] not in str(r), s)
    s, r = call("GET", f"/staff/{sid}/bank", token=ca, campus=a)
    check("bank endpoint returns full value", s == 200 and r["data"]["bankAccount"] == body["bankAccount"], (s, r))

    s, r = call("POST", "/staff", staff_body(), token=hq)
    check("no working campus -> 400", s == 400 and r.get("message") == "Select a campus.", (s, r))
    s, r = call("POST", "/staff", staff_body(cnic=body["cnic"]), token=ca, campus=a)
    check("CNIC of active staff -> 409", s == 409 and r.get("message") == "This CNIC belongs to an active staff member.", (s, r))
    s, r = call("POST", "/staff", staff_body(cnic=rand_digits(12)), token=ca, campus=a)
    check("12-digit CNIC -> 400", s == 400, (s, r))
    s, r = call("POST", "/staff", staff_body(basicSalary=0), token=ca, campus=a)
    check("salary 0 -> 400", s == 400, (s, r))
    s, r = call("POST", "/staff", staff_body(firstName="Y" * 101), token=ca, campus=a)
    check("first name over 100 -> 400", s == 400, (s, r))

    edit = dict(body, address="New address", bankAccount=None)
    s, r = call("PUT", f"/staff/{sid}", edit, token=ca, campus=a)
    check("edit keeps bank when null", s == 200 and r["data"]["personal"]["address"] == "New address"
          and r["data"]["personal"].get("bankAccountMasked") == "****" + body["bankAccount"][-4:], (s, r))
    s, r = call("PUT", f"/staff/{sid}", dict(body, bankAccount=""), token=ca, campus=a)
    check("edit clears bank with empty string", s == 200 and not r["data"]["personal"].get("bankAccountMasked"), (s, r))

    t_id, _ = make_staff(ca, a, category="Teaching", designation="Teacher")
    items = data(call("GET", "/staff?category=NonTeaching&pageSize=100", token=ca, campus=a))["items"]
    check("category filter", any(x["id"] == sid for x in items) and not any(x["id"] == t_id for x in items), len(items))

    s, _ = call("DELETE", f"/staff/{t_id}", token=ca, campus=a)
    check("delete a fresh direct member", s == 200, s)
    if s == 200:
        _created["staff"].remove(t_id)

    # a hired member cannot be deleted
    iv = interviewer(hq, a)
    vid, _ = make_vacancy(ca, a, positions=1)
    cid, _ = make_candidate(ca, a, vid)
    to_offer_accepted(ca, a, cid, iv)
    s, r = call("POST", f"/candidates/{cid}/hire", hire_body(), token=ca, campus=a)
    if s == 200:
        hired = track("staff", r["data"]["id"])
        s, _ = call("DELETE", f"/staff/{hired}", token=ca, campus=a)
        check("delete a hired member -> 400", s == 400, s)
    retire_staff(hq)
    retire_candidates(ca)


# ------------------------------------------------------------------ hire rules

@group
def hire_rules(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    iv = interviewer(hq, a)
    vid, _ = make_vacancy(ca, a, positions=1)
    c1, _ = make_candidate(ca, a, vid, qualification="M.Phil Physics", experienceYears=6)
    c2, _ = make_candidate(ca, a, vid)
    to_offer_accepted(ca, a, c1, iv)
    to_offer_accepted(ca, a, c2, iv)
    s, r = call("POST", f"/candidates/{c1}/hire", hire_body(), token=ca, campus=a)
    ok = check("first hire", s == 200, (s, r))
    if ok:
        sid = track("staff", r["data"]["id"])
        p = data(call("GET", f"/staff/{sid}", token=ca, campus=a))
        check("qualification and experience copied on hire", p["personal"].get("qualification") == "M.Phil Physics"
              and p["personal"].get("experienceYears") == 6, p["personal"])
    s, r = call("POST", f"/candidates/{c2}/hire", hire_body(), token=ca, campus=a)
    check("hire_full_vacancy_refused", s == 400 and r.get("message") == "All positions are filled. Raise the positions first.", (s, r))
    s, _ = call("POST", f"/vacancies/{vid}/status", {"status": "OnHold"}, token=ca, campus=a)
    s, r = call("POST", f"/vacancies/{vid}/status", {"status": "Open"}, token=ca, campus=a)
    check("reopen_needs_free_position: Filled -> OnHold -> Open refused", s == 400, (s, r))

    vid2, _ = make_vacancy(ca, a, positions=3)
    c3, _ = make_candidate(ca, a, vid2)
    to_offer_accepted(ca, a, c3, iv)
    call("POST", f"/vacancies/{vid2}/status", {"status": "Closed"}, token=ca, campus=a)
    s, r = call("POST", f"/candidates/{c3}/hire", hire_body(), token=ca, campus=a)
    check("hire_closed_vacancy_refused", s == 400 and r.get("message") == "This vacancy is not open.", (s, r))
    retire_candidates(ca)


# ------------------------------------------------------------------ documents

@group
def documents(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sid, _ = make_staff(ca, a)
    s, r = upload(f"/staff/{sid}/documents", {"type": "Cnic"}, "file", "cnic scan.pdf", PDF, "application/pdf", ca, a)
    ok = check("upload CNIC PDF", s == 200 and r["data"]["type"] == "Cnic", (s, r))
    if not ok:
        return
    doc = r["data"]["id"]
    p = data(call("GET", f"/staff/{sid}", token=ca, campus=a))
    check("document listed in profile", any(d["id"] == doc for d in p["documents"]), p["documents"])
    s, body, headers = download(f"/staff/{sid}/documents/{doc}", ca, a)
    check("download byte-identical with name", s == 200 and body == PDF and "cnic" in headers.get("Content-Disposition", ""), s)
    s, r = upload(f"/staff/{sid}/documents", {"type": "Other"}, "file", "evil.pdf", b"MZ\x90\x00" + b"\x00" * 64, "application/pdf", ca, a)
    check("MZ renamed .pdf -> 400", s == 400, (s, r))
    s, r = upload(f"/staff/{sid}/documents", {"type": "Other"}, "file", "cv.docx", b"PK\x03\x04" + b"\x00" * 64, "application/octet-stream", ca, a)
    check(".docx not allowed for staff -> 400", s == 400, (s, r))
    s, r = upload(f"/staff/{sid}/documents", {"type": "Other"}, "file", "big.pdf", PDF + b"\x00" * (10_485_761 - len(PDF)), "application/pdf", ca, a)
    check("10,485,761 bytes -> 400", s in (400, 413), s)
    s, _ = call("DELETE", f"/staff/{sid}/documents/{doc}", token=ca, campus=a)
    p = data(call("GET", f"/staff/{sid}", token=ca, campus=a))
    check("delete document", s == 200 and not p["documents"], (s, p["documents"]))
    s, body, _ = download(f"/staff/{sid}/documents/{doc}", ca, a)
    check("deleted document -> 404", s == 404, s)
    s, r = upload(f"/staff/{sid}/photo", {}, "file", "me.png", PNG, "image/png", ca, a)
    p = data(call("GET", f"/staff/{sid}", token=ca, campus=a))
    check("photo uploaded", s == 200 and p["personal"]["hasPhoto"], (s, r))
    s, body, _ = download(f"/staff/{sid}/photo", ca, a)
    check("photo download", s == 200 and body == PNG, s)
    s, r = upload(f"/staff/{sid}/photo", {}, "file", "big.png", PNG + b"\x00" * (2_097_153 - len(PNG)), "image/png", ca, a)
    check("photo 2,097,153 bytes -> 400", s == 400, s)
    s, r = upload(f"/staff/{sid}/photo", {}, "file", "doc.pdf", PDF, "application/pdf", ca, a)
    check("PDF as photo -> 400", s == 400, s)


# ------------------------------------------------------------------ checklist

def tick(token, campus, sid, item, done=True):
    return call("POST", f"/staff/{sid}/checklist/{item}", {"done": done}, token=token, campus=campus)


@group
def checklist(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    tid, p = make_staff(ca, a, category="Teaching", designation="Teacher")
    c = p["checklist"]
    check("teaching member starts with CNIC and Degree missing", not c["documentsReceived"] and c["missingDocuments"] == ["Cnic", "Degree"]
          and c["doneCount"] == 0, c)
    upload(f"/staff/{tid}/documents", {"type": "Cnic"}, "file", "c.pdf", PDF, "application/pdf", ca, a)
    c = data(call("GET", f"/staff/{tid}", token=ca, campus=a))["checklist"]
    check("documents_rule_by_category: teaching with CNIC only still misses Degree", not c["documentsReceived"]
          and c["missingDocuments"] == ["Degree"], c)
    nid, _ = make_staff(ca, a)
    upload(f"/staff/{nid}/documents", {"type": "Cnic"}, "file", "c.pdf", PDF, "application/pdf", ca, a)
    c = data(call("GET", f"/staff/{nid}", token=ca, campus=a))["checklist"]
    check("documents_rule_by_category: non-teaching done with CNIC", c["documentsReceived"] and c["missingDocuments"] == [], c)
    for item in ("documents", "login", "nonsense"):
        s, _ = tick(ca, a, nid, item)
        check(f"manual tick of '{item}' -> 400", s == 400, s)
    tick(ca, a, nid, "contract")
    s, r = tick(ca, a, nid, "orientation")
    check("three of four done, still Onboarding", s == 200 and r["data"]["checklist"]["doneCount"] == 3
          and r["data"]["checklist"]["contractSigned"] and r["data"]["checklist"].get("orientationDoneOn")
          and r["data"]["staff"]["status"] == "Onboarding", (s, r))
    s, r = tick(ca, a, nid, "contract", False)
    check("untick contract", s == 200 and r["data"]["checklist"]["doneCount"] == 2 and not r["data"]["checklist"]["contractSigned"], (s, r))


# ------------------------------------------------------------------ account

def login_body(email=None, role="Clerk", hq=None):
    return {"email": email or f"qa.staff.{rand_digits(6)}@example.test", "password": temp_password(), "roleId": role_id(hq, role)}


@group
def account(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    sid, _ = make_staff(ca, a)
    upload(f"/staff/{sid}/documents", {"type": "Cnic"}, "file", "c.pdf", PDF, "application/pdf", ca, a)
    tick(ca, a, sid, "contract")
    tick(ca, a, sid, "orientation")
    body = login_body(role="Clerk", hq=hq)
    s, r = call("POST", f"/staff/{sid}/account", body, token=ca, campus=a)
    ok = check("create login", s == 200 and r["data"]["account"]["mustChangePassword"] and r["data"]["account"]["email"] == body["email"]
               and r["data"]["account"]["roles"] == ["Clerk"] and r["data"]["checklist"]["loginCreated"], (s, r))
    if not ok:
        return
    track("users", r["data"]["account"]["userId"])
    check("all four done -> Active", r["data"]["staff"]["status"] == "Active" and r["data"]["checklist"]["doneCount"] == 4, r["data"]["staff"])
    auth = login(body["email"], body["password"])
    check("login works and must change password", auth["user"]["mustChangePassword"]
          and [c["id"] for c in auth["user"].get("campuses", [])] in ([a], []), auth["user"])
    s, r = tick(ca, a, sid, "orientation", False)
    check("untick after Active stays Active", s == 200 and r["data"]["staff"]["status"] == "Active", (s, r))
    s, r = call("POST", f"/staff/{sid}/account", login_body(hq=hq), token=ca, campus=a)
    check("second login -> 409", s == 409 and r.get("message") == "This staff member already has a login.", (s, r))

    sid2, _ = make_staff(ca, a)
    s, r = call("POST", f"/staff/{sid2}/account", login_body(role="CampusAdmin", hq=hq), token=ca, campus=a)
    check("account_role_escalation_refused: Main-Campus-only role -> 403", s == 403, (s, r))
    s, rr = create_role(hq, "QA B Role " + rand_digits(4), campus_id=b)
    s, r = call("POST", f"/staff/{sid2}/account", {"email": f"qa.staff.{rand_digits(6)}@example.test", "password": temp_password(),
                                                    "roleId": rr["data"]["id"]}, token=ca, campus=a)
    check("account_role_escalation_refused: campus-B role -> 403", s == 403, (s, r))
    s, r = call("POST", f"/staff/{sid2}/account", login_body(email=body["email"], hq=hq), token=ca, campus=a)
    check("email already used -> 409", s == 409, (s, r))

    uid, _, _ = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    s, r = call("POST", f"/staff/{sid2}/account/link", {"userId": uid}, token=ca, campus=a)
    check("link an unlinked campus user", s == 200 and r["data"]["account"]["userId"] == uid, (s, r))
    sid3, _ = make_staff(ca, a)
    s, r = call("POST", f"/staff/{sid3}/account/link", {"userId": uid}, token=ca, campus=a)
    check("link a user linked to other staff -> 409", s == 409, (s, r))
    ub, _, _ = make_user(hq, campus_ids=[b], role_names=["Teacher"])
    s, r = call("POST", f"/staff/{sid3}/account/link", {"userId": ub}, token=ca, campus=a)
    check("link a campus-B user -> 400", s == 400, (s, r))

    _, pe, pp = make_user(hq, campus_ids=[a], role_names=["Principal"])
    principal = login(pe, pp)["tokens"]["accessToken"]
    s, r = call("POST", f"/staff/{sid3}/account", login_body(role="Teacher", hq=hq), token=principal, campus=a)
    check("Principal can create a login", s == 200, (s, r))
    if s == 200:
        track("users", r["data"]["account"]["userId"])
    s, rr = create_role(hq, "QA Staff Only " + rand_digits(4))
    call("PUT", f"/roles/{rr['data']['id']}", {"name": rr["data"]["name"], "isActive": True,
                                              "matrix": full_matrix(["staff"])}, token=hq)
    uid4, e4, p4 = make_user(hq, campus_ids=[a], role_ids=[rr["data"]["id"]])
    staff_only = login(e4, p4)["tokens"]["accessToken"]
    sid4, _ = make_staff(ca, a)
    s, r = call("POST", f"/staff/{sid4}/account", login_body(hq=hq), token=staff_only, campus=a)
    check("staff:Edit without users:Add -> 403", s == 403, (s, r))


# ------------------------------------------------------------------ assignments

def section_of(token, campus, class_code, name):
    rows = data(call("GET", "/sections", token=token, campus=campus)) or []
    return next(x for x in rows if x["classCode"] == class_code and x["name"] == name)


def class_subjects(token, campus, class_id):
    return data(call("GET", f"/classes/{class_id}", token=token, campus=campus))["subjects"]


def assign(token, campus, sid, section_id, subject_ids):
    return call("POST", f"/staff/{sid}/assignments", {"sectionId": section_id, "subjectIds": subject_ids}, token=token, campus=campus)


@group
def assignments(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    t1, p1 = make_staff(ca, a, category="Teaching", designation="Teacher", firstName="Qa", lastName="Teacher One")
    t2, _ = make_staff(ca, a, category="Teaching", designation="Teacher", firstName="Qa", lastName="Teacher Two")
    s3, s4, s5 = section_of(ca, a, "G3", "A"), section_of(ca, a, "G4", "B"), section_of(ca, a, "G5", "A")
    subj3 = class_subjects(ca, a, s3["classId"])
    subj4 = class_subjects(ca, a, s4["classId"])
    subj5 = class_subjects(ca, a, s5["classId"])
    first = subj3[0]
    for sec, sub in ((s3, subj3[0]), (s4, subj4[1]), (s5, subj5[2])):
        s, r = assign(ca, a, t1, sec["id"], [sub["subjectId"]])
    check("one teacher, three classes, different subjects", s == 200 and len(r["data"]["assignments"]) == 3, (s, r))
    s, r = assign(ca, a, t1, s3["id"], [first["subjectId"]])
    check("repeating own pair adds nothing", s == 200 and len(r["data"]["assignments"]) == 3, (s, r))
    s, r = assign(ca, a, t2, s3["id"], [first["subjectId"]])
    msg = r.get("message") or "" if isinstance(r, dict) else ""
    check("second teacher for the same section subject -> 409 naming the teacher", s == 409 and "already taught by Qa Teacher One" in msg, (s, r))
    all_subjects = data(call("GET", "/subjects", token=ca, campus=a))
    outside = next((x for x in all_subjects if x["id"] not in {c["subjectId"] for c in subj3}), None)
    if outside:
        s, r = assign(ca, a, t2, s3["id"], [outside["id"]])
        check("subject not in the class -> 400", s == 400, (s, r))
    nt, _ = make_staff(ca, a)
    s, r = assign(ca, a, nt, s3["id"], [subj3[1]["subjectId"]])
    check("non-teaching staff -> 400", s == 400, (s, r))
    s, r = call("GET", f"/staff/{t2}/assignments/options", token=ca, campus=a)
    opt = next((x for sec in (r["data"]["sections"] if s == 200 else []) if sec["sectionId"] == s3["id"]
                for x in sec["subjects"] if x["subjectId"] == first["subjectId"]), None)
    check("options show the current teacher", s == 200 and opt and opt.get("teacherName") == "Qa Teacher One", (s, opt))

    # concurrent_assignment_one_teacher
    pair = subj4[0]["subjectId"]
    t3, _ = make_staff(ca, a, category="Teaching", designation="Teacher")
    codes = []

    def go(tid):
        codes.append(assign(ca, a, tid, s4["id"], [pair])[0])

    threads = [_threading.Thread(target=go, args=(t,)) for t in (t2, t3)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    teachers = data(call("GET", f"/sections/{s4['id']}/teachers", token=ca, campus=a))
    holders = [x for x in teachers if x["subjectId"] == pair and x.get("teacherStaffId")]
    check("concurrent_assignment_one_teacher", sorted(codes) == [200, 409] and len(holders) == 1, (codes, holders))

    p = data(call("GET", f"/staff/{t1}", token=ca, campus=a))
    aid = next(x["id"] for x in p["assignments"] if x["sectionId"] == s3["id"])
    s, r = call("DELETE", f"/staff/{t1}/assignments/{aid}", token=ca, campus=a)
    check("end assignment", s == 200 and len(r["data"]["assignments"]) == 2, (s, r))
    s, r = assign(ca, a, t2, s3["id"], [first["subjectId"]])
    check("pair free again after ending", s == 200, (s, r))

    s, r = call("PUT", f"/sections/{s5['id']}/class-teacher", {"staffId": t1}, token=ca, campus=a)
    sec = section_of(ca, a, "G5", "A")
    check("set class teacher", s == 200 and sec.get("classTeacherName") == "Qa Teacher One", (s, sec))
    p = data(call("GET", f"/staff/{t1}", token=ca, campus=a))
    check("profile lists class-teacher sections", any(x["sectionId"] == s5["id"] for x in p["classTeacherOf"]), p["classTeacherOf"])
    s, r = call("PUT", f"/sections/{s5['id']}/class-teacher", {"staffId": nt}, token=ca, campus=a)
    check("non-teaching class teacher -> 400", s == 400, (s, r))
    s, r = call("PUT", f"/sections/{s5['id']}/class-teacher", {"staffId": None}, token=ca, campus=a)
    check("clear class teacher", s == 200 and not section_of(ca, a, "G5", "A").get("classTeacherName"), (s, r))
    teachers = data(call("GET", f"/sections/{s3['id']}/teachers", token=ca, campus=a))
    check("section teachers list every class subject", len(teachers) == len(subj3)
          and any(x["subjectId"] == first["subjectId"] and x.get("teacherName") for x in teachers), teachers)
    # free the sections for later runs
    for tid in (t1, t2, t3):
        for x in data(call("GET", f"/staff/{tid}", token=ca, campus=a))["assignments"]:
            call("DELETE", f"/staff/{tid}/assignments/{x['id']}", token=ca, campus=a)


# ------------------------------------------------------------------ lifecycle

def set_status(token, campus, sid, status, date=None, reason=None):
    return call("POST", f"/staff/{sid}/status", {"status": status, "date": date, "reason": reason}, token=token, campus=campus)


def activate(token, campus, sid, hq, role="Teacher"):
    """CNIC + Degree, contract, orientation, login -> Active. Returns (email, password)."""
    upload(f"/staff/{sid}/documents", {"type": "Cnic"}, "file", "c.pdf", PDF, "application/pdf", token, campus)
    upload(f"/staff/{sid}/documents", {"type": "Degree"}, "file", "d.pdf", PDF, "application/pdf", token, campus)
    tick(token, campus, sid, "contract")
    tick(token, campus, sid, "orientation")
    body = login_body(role=role, hq=hq)
    s, r = call("POST", f"/staff/{sid}/account", body, token=token, campus=campus)
    if s == 200:
        track("users", r["data"]["account"]["userId"])
    return body["email"], body["password"]


@group
def lifecycle(hq):
    a, _ = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    fresh, p = make_staff(ca, a, category="Teaching", designation="Teacher")
    check("onboarding member may only resign or be terminated", p["allowedStatuses"] == ["Resigned", "Terminated"], p["allowedStatuses"])
    s, r = set_status(ca, a, fresh, "OnLeave")
    check("Onboarding -> OnLeave refused, listing allowed moves", s == 400 and "Resigned" in (r.get("message") or ""), (s, r))

    sid, p = make_staff(ca, a, category="Teaching", designation="Teacher", firstName="Qa", lastName="Leaver")
    email, pw = activate(ca, a, sid, hq)
    p = data(call("GET", f"/staff/{sid}", token=ca, campus=a))
    ok = check("activated", p["staff"]["status"] == "Active", p["staff"]["status"])
    if not ok:
        return
    s, r = set_status(ca, a, sid, "OnLeave")
    check("Active -> OnLeave", s == 200 and r["data"]["staff"]["status"] == "OnLeave", (s, r))
    s, r = set_status(ca, a, sid, "Active")
    check("OnLeave -> Active", s == 200 and r["data"]["staff"]["status"] == "Active", (s, r))

    sec = section_of(ca, a, "G2", "B")
    free = [x for x in data(call("GET", f"/sections/{sec['id']}/teachers", token=ca, campus=a)) if not x.get("teacherStaffId")]
    subj = free[0]["subjectId"]
    s, r = assign(ca, a, sid, sec["id"], [subj])
    check("assigned before leaving", s == 200 and len(r["data"]["assignments"]) == 1, (s, r))
    call("PUT", f"/sections/{sec['id']}/class-teacher", {"staffId": sid}, token=ca, campus=a)
    user_token = login(email, pw)["tokens"]["accessToken"]

    s, r = set_status(ca, a, sid, "Resigned", today())
    check("resign without reason -> 400", s == 400 and r.get("message") == "A reason is required.", (s, r))
    s, r = set_status(ca, a, sid, "Resigned", "2000-01-01", "Moved")
    check("leaving date before joining -> 400", s == 400, (s, r))
    s, r = set_status(ca, a, sid, "Resigned", today(), "Moved to another city")
    ok = check("resign", s == 200 and r["data"]["staff"]["status"] == "Resigned" and r["data"]["personal"]["leftOn"] == today()
               and r["data"]["personal"]["leaveReason"] == "Moved to another city", (s, r))
    if ok:
        check("resign ends assignments", r["data"]["assignments"] == [] and r["data"]["classTeacherOf"] == [], r["data"]["assignments"])
        check("resign deactivates the login", r["data"]["account"] and not r["data"]["account"]["isActive"], r["data"]["account"])
    s, _ = call("GET", "/auth/me", token=user_token)
    check("resign_blocks_login: old token refused", s in (401, 403), s)
    s, _ = call("POST", "/auth/login", {"email": email, "password": pw})
    check("resign_blocks_login: password login refused", s in (400, 401, 403), s)
    check("class teacher cleared", not section_of(ca, a, "G2", "B").get("classTeacherName"), section_of(ca, a, "G2", "B"))
    teachers = data(call("GET", f"/sections/{sec['id']}/teachers", token=ca, campus=a))
    check("section subject free again", not any(x["subjectId"] == subj and x.get("teacherStaffId") for x in teachers), teachers)
    s, r = set_status(ca, a, sid, "Active")
    check("Resigned is final", s == 400, (s, r))
    s, r = call("POST", f"/staff/{sid}/checklist/contract", {"done": True}, token=ca, campus=a)
    check("no checklist changes after leaving", s == 400, (s, r))

    # rehire_same_cnic
    cnic = data(call("GET", f"/staff/{sid}", token=ca, campus=a))["staff"]["cnic"]
    s, r = call("POST", "/staff", staff_body(cnic=cnic), token=ca, campus=a)
    check("rehire_same_cnic: direct onboarding with a resigned member's CNIC", s == 200, (s, r))
    if s == 200:
        track("staff", r["data"]["staff"]["id"])


# ------------------------------------------------------------------ isolation & permissions

@group
def onboarding_isolation(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    bid, before = make_staff(hq, b, category="Teaching", designation="Teacher")
    upload(f"/staff/{bid}/photo", {}, "file", "b.png", PNG, "image/png", hq, b)
    s, r = upload(f"/staff/{bid}/documents", {"type": "Cnic"}, "file", "b.pdf", PDF, "application/pdf", hq, b)
    doc = r["data"]["id"]
    secb = section_of(hq, b, "G1", "A")
    free = [x for x in data(call("GET", f"/sections/{secb['id']}/teachers", token=hq, campus=b)) if not x.get("teacherStaffId")]
    s, r = assign(hq, b, bid, secb["id"], [free[0]["subjectId"]])
    aid = r["data"]["assignments"][0]["id"] if s == 200 else 0
    seca = section_of(ca, a, "G1", "A")
    attempts = [
        ("GET", call("GET", f"/staff/{bid}", token=ca, campus=a)[0]),
        ("PUT", call("PUT", f"/staff/{bid}", staff_body(firstName="Hacked"), token=ca, campus=a)[0]),
        ("DELETE", call("DELETE", f"/staff/{bid}", token=ca, campus=a)[0]),
        ("bank", call("GET", f"/staff/{bid}/bank", token=ca, campus=a)[0]),
        ("photo", download(f"/staff/{bid}/photo", ca, a)[0]),
        ("photo upload", upload(f"/staff/{bid}/photo", {}, "file", "x.png", PNG, "image/png", ca, a)[0]),
        ("doc download", download(f"/staff/{bid}/documents/{doc}", ca, a)[0]),
        ("doc upload", upload(f"/staff/{bid}/documents", {"type": "Other"}, "file", "x.pdf", PDF, "application/pdf", ca, a)[0]),
        ("doc delete", call("DELETE", f"/staff/{bid}/documents/{doc}", token=ca, campus=a)[0]),
        ("checklist", call("POST", f"/staff/{bid}/checklist/contract", {"done": True}, token=ca, campus=a)[0]),
        ("account", call("POST", f"/staff/{bid}/account", login_body(hq=hq), token=ca, campus=a)[0]),
        ("account link", call("POST", f"/staff/{bid}/account/link", {"userId": 1}, token=ca, campus=a)[0]),
        ("options", call("GET", f"/staff/{bid}/assignments/options", token=ca, campus=a)[0]),
        ("assign", assign(ca, a, bid, seca["id"], [1])[0]),
        ("end assignment", call("DELETE", f"/staff/{bid}/assignments/{aid}", token=ca, campus=a)[0]),
        ("status", set_status(ca, a, bid, "Resigned", today(), "x")[0]),
        ("class teacher on B section", call("PUT", f"/sections/{secb['id']}/class-teacher", {"staffId": None}, token=ca, campus=a)[0]),
        ("B section teachers", call("GET", f"/sections/{secb['id']}/teachers", token=ca, campus=a)[0]),
    ]
    check("every cross-campus staff action -> 404", all(code == 404 for _, code in attempts), attempts)
    s, r = call("PUT", f"/sections/{seca['id']}/class-teacher", {"staffId": bid}, token=ca, campus=a)
    check("campus-B staff as class teacher of a campus-A section -> 400", s == 400, (s, r))
    items = data(call("GET", "/staff?pageSize=100", token=ca, campus=a))["items"]
    check("list hides campus-B staff", not any(x["id"] == bid for x in items), len(items))
    after = data(call("GET", f"/staff/{bid}", token=hq, campus=b))
    check("campus-B staff unchanged", after["staff"]["firstName"] == before["staff"]["firstName"] and after["staff"]["status"] == "Onboarding"
          and len(after["documents"]) == 1 and len(after["assignments"]) == 1 and not after["checklist"]["contractSigned"]
          and not after.get("account"), after["staff"])
    for x in after["assignments"]:
        call("DELETE", f"/staff/{bid}/assignments/{x['id']}", token=hq, campus=b)


@group
def onboarding_permissions(hq):
    a, _ = demo_campuses(hq)
    sid, _ = make_staff(hq, a)
    for role in ("Teacher", "Clerk", "Accountant"):
        _, e, p = make_user(hq, campus_ids=[a], role_names=[role])
        tok = login(e, p)["tokens"]["accessToken"]
        codes = [call("GET", "/staff", token=tok, campus=a)[0], call("GET", f"/staff/{sid}", token=tok, campus=a)[0],
                 call("POST", "/staff", staff_body(), token=tok, campus=a)[0],
                 call("POST", f"/staff/{sid}/status", {"status": "Resigned", "date": today(), "reason": "x"}, token=tok, campus=a)[0],
                 call("GET", f"/staff/{sid}/bank", token=tok, campus=a)[0]]
        check(f"{role}: staff endpoints -> 403", all(c == 403 for c in codes), codes)
    _, e, p = make_user(hq, campus_ids=[a], role_names=["Principal"])
    tok = login(e, p)["tokens"]["accessToken"]
    s, r = call("POST", "/staff", staff_body(), token=tok, campus=a)
    check("Principal can add staff", s == 200, (s, r))
    if s == 200:
        nid = track("staff", r["data"]["staff"]["id"])
        s, r = call("POST", f"/staff/{nid}/account", login_body(role="Clerk", hq=hq), token=tok, campus=a)
        check("Principal can create a login", s == 200, (s, r))
        if s == 200:
            track("users", r["data"]["account"]["userId"])


# ------------------------------------------------------------------ review fixes

@group
def onboarding_review_fixes(hq):
    from academics_e2e import create_session, session_body, sessions_list
    a, b = demo_campuses(hq)
    ca_id, ca = campus_admin(hq, a)

    # C1 link_guarded: an HR user (staff + users:Add/View) cannot link a campus admin, a multi-campus user or themselves
    s, rr = create_role(hq, "QA HR " + rand_digits(4))
    call("PUT", f"/roles/{rr['data']['id']}", {"name": rr["data"]["name"], "isActive": True,
                                              "matrix": full_matrix(["staff"]) + full_matrix(["users"], edit=False, delete=False)}, token=hq)
    hr_id, he, hp = make_user(hq, campus_ids=[a], role_ids=[rr["data"]["id"]])
    hr = login(he, hp)["tokens"]["accessToken"]
    dummy, _ = make_staff(hr, a)
    s, r = call("POST", f"/staff/{dummy}/account/link", {"userId": ca_id}, token=hr, campus=a)
    check("link_guarded: campus admin user -> 403", s == 403, (s, r))
    both, _, _ = make_user(hq, campus_ids=[a, b], role_names=["Teacher"])
    s, r = call("POST", f"/staff/{dummy}/account/link", {"userId": both}, token=hr, campus=a)
    check("link_guarded: user of another campus too -> 403", s == 403, (s, r))
    s, r = call("POST", f"/staff/{dummy}/account/link", {"userId": hr_id}, token=hr, campus=a)
    check("link_guarded: own account -> 403", s == 403, (s, r))

    # C1 resign_guarded: HQ links a campus admin user; the campus admin of A cannot resign that record (login untouched)
    ca2_id, _ = campus_admin(hq, a)
    victim, _ = make_staff(hq, a)
    s, r = call("POST", f"/staff/{victim}/account/link", {"userId": ca2_id}, token=hq, campus=a)
    check("HQ may link a campus admin user", s == 200, (s, r))
    s, r = set_status(hr, a, victim, "Resigned", today(), "x")
    check("resign_guarded: HR cannot resign a member linked to a campus admin -> 403", s == 403, (s, r))
    users = data(call("GET", "/users", token=hq))
    check("resign_guarded: campus admin login still active", next(u for u in users if u["id"] == ca2_id)["isActive"], ca2_id)
    call("POST", f"/staff/{victim}/status", {"status": "Resigned", "date": today(), "reason": "qa"}, token=hq, campus=a)

    # I1 relink_after_rehire: resign with login, reactivate the user, rehire with same CNIC, link the old user
    old, p = make_staff(ca, a)
    cnic = p["staff"]["cnic"]
    body = login_body(role="Clerk", hq=hq)
    s, r = call("POST", f"/staff/{old}/account", body, token=ca, campus=a)
    uid = track("users", r["data"]["account"]["userId"]) if s == 200 else 0
    set_status(ca, a, old, "Resigned", today(), "left")
    from sms_client import user_payload, role_id as rid
    payload = user_payload(campus_ids=[a], role_ids=[rid(hq, "Clerk")], email=body["email"], password=body["password"])
    s, _ = call("PUT", f"/users/{uid}", payload, token=hq)
    new, _ = make_staff(ca, a, cnic=cnic)
    s, r = call("POST", f"/staff/{new}/account/link", {"userId": uid}, token=ca, campus=a)
    check("relink_after_rehire: old login links to the rehired record", s == 200, (s, r))

    # I4 delete_class_teacher_refused
    t1, _ = make_staff(ca, a, category="Teaching", designation="Teacher")
    sec = section_of(ca, a, "G1", "B")
    call("PUT", f"/sections/{sec['id']}/class-teacher", {"staffId": t1}, token=ca, campus=a)
    s, r = call("DELETE", f"/staff/{t1}", token=ca, campus=a)
    check("delete_class_teacher_refused", s == 400, (s, r))
    call("PUT", f"/sections/{sec['id']}/class-teacher", {"staffId": None}, token=ca, campus=a)

    # I5 class_teacher_options: server-side list for the section's campus, current teaching staff only
    s, r = call("GET", f"/sections/{sec['id']}/class-teacher/options", token=hq)
    ids = [x["id"] for x in r["data"]] if s == 200 else []
    check("class_teacher_options: includes the campus's teacher (HQ, all campuses)", s == 200 and t1 in ids, (s, r))
    check("class_teacher_options: excludes non-teaching and left staff", dummy not in ids and old not in ids, ids)

    # I3 section_delete_ends_assignments
    t2, _ = make_staff(ca, a, category="Teaching", designation="Teacher")
    cls = section_of(ca, a, "G1", "A")["classId"]
    s, r = call("POST", "/sections", {"classId": cls, "academicSessionId": None, "name": "QZ" + rand_digits(2), "shift": "Morning",
                                     "capacity": None, "room": None, "isActive": True}, token=ca, campus=a)
    qsec = r["data"]["id"] if s == 200 else 0
    subj = class_subjects(ca, a, cls)[0]["subjectId"]
    assign(ca, a, t2, qsec, [subj])
    call("PUT", f"/sections/{qsec}/class-teacher", {"staffId": t2}, token=ca, campus=a)
    s, _ = call("DELETE", f"/sections/{qsec}", token=ca, campus=a)
    p2 = data(call("GET", f"/staff/{t2}", token=ca, campus=a))
    check("section_delete_ends_assignments: profile clean", s == 200 and p2["assignments"] == [] and p2["classTeacherOf"] == [], (s, p2["assignments"]))
    s, r = call("PUT", f"/staff/{t2}", staff_body(category="NonTeaching", designation="Clerk", cnic=p2["staff"].get("cnic")), token=ca, campus=a)
    check("section_delete_ends_assignments: category change allowed", s == 200, (s, r))

    # I2 assignments_scoped_to_current_session
    t3, _ = make_staff(ca, a, category="Teaching", designation="Teacher")
    sec3 = section_of(ca, a, "G2", "A")
    free = [x for x in data(call("GET", f"/sections/{sec3['id']}/teachers", token=ca, campus=a)) if not x.get("teacherStaffId")]
    assign(ca, a, t3, sec3["id"], [free[0]["subjectId"]])
    before = sessions_list(ca, a)["effectiveCurrentId"]
    s, r = create_session(ca, session_body("QA-NX" + rand_digits(3), "2098-04-01", "2099-03-31"), campus=a)
    nx = r["data"]["id"] if s == 200 else None
    if nx:
        call("POST", f"/academic-sessions/{nx}/set-current", token=ca, campus=a)
        p3 = data(call("GET", f"/staff/{t3}", token=ca, campus=a))
        check("assignments_scoped_to_current_session: last session's classes not listed", p3["assignments"] == [], p3["assignments"])
        s, r = call("PUT", f"/staff/{t3}", staff_body(category="NonTeaching", designation="Clerk", cnic=p3["staff"].get("cnic")), token=ca, campus=a)
        check("assignments_scoped_to_current_session: category change not blocked by old session", s == 200, (s, r))
        call("POST", f"/academic-sessions/{before}/set-current", token=ca, campus=a)
        call("DELETE", f"/academic-sessions/{nx}", token=ca, campus=a)
    for x in data(call("GET", f"/staff/{t3}", token=ca, campus=a))["assignments"]:
        call("DELETE", f"/staff/{t3}/assignments/{x['id']}", token=ca, campus=a)


# ------------------------------------------------------------------ list-filters (designation, sort, designations list)

def _all_staff(token, campus):
    rows, page = [], 1
    while True:
        r = data(call("GET", f"/staff?pageSize=100&page={page}", token=token, campus=campus))
        rows += r["items"]
        if len(rows) >= r["total"] or not r["items"]:
            return rows
        page += 1


@group
def list_filters(hq):
    from urllib.parse import quote as _quote
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    _, cb = campus_admin(hq, b)
    tag = "Lf" + rand_digits(5)
    ids = {}
    for letter, desig, joined in (("A", "Clerk", -100), ("B", "Librarian", -10), ("C", "Clerk", -50), ("D", "Accountant", -30)):
        ids[letter], _ = make_staff(ca, a, lastName=tag + letter, designation=desig, joiningDate=today(joined))
    by_id = {v: k for k, v in ids.items()}

    def get(token=ca, campus=a, **params):
        qs = "&".join(f"{k}={_quote(str(v))}" for k, v in dict(q=tag, **params).items())
        s, r = call("GET", f"/staff?{qs}", token=token, campus=campus)
        return s, (r["data"] if s == 200 else r)

    def letters(page):
        return [by_id.get(x["id"], "?") for x in page["items"]]

    s, p = get()
    check("staff default: name order unchanged", s == 200 and p["total"] == 4 and letters(p) == ["A", "B", "C", "D"], (s, p))
    s, p = get(designation="Clerk")
    check("designation filter narrows", s == 200 and p["total"] == 2 and letters(p) == ["A", "C"], (s, p))
    s, p = get(designation="clerk")
    check("designation filter is case-insensitive", s == 200 and letters(p) == ["A", "C"], (s, p))
    s, p = get(designation="Astronaut")
    check("unknown designation -> 400", s == 400, (s, p))

    s, p = get(sort="joined")
    check("sort joined asc", s == 200 and letters(p) == ["A", "C", "D", "B"], (s, p))
    s, p = get(sort="joined", dir="desc")
    check("sort joined desc", s == 200 and letters(p) == ["B", "D", "C", "A"], (s, p))
    s, p = get(sort="designation")
    check("sort designation asc", s == 200 and [x["designation"] for x in p["items"]] == ["Accountant", "Clerk", "Clerk", "Librarian"], (s, p))
    s, p = get(sort="designation", dir="desc")
    check("sort designation desc", s == 200 and [x["designation"] for x in p["items"]] == ["Librarian", "Clerk", "Clerk", "Accountant"], (s, p))
    s, p = get(sort="employeeNo")
    nos = [x["employeeNo"] for x in p["items"]] if s == 200 else []
    check("sort employeeNo asc", len(nos) == 4 and nos == sorted(nos), nos)
    s, p = get(sort="employeeNo", dir="desc")
    check("sort employeeNo desc", s == 200 and [x["employeeNo"] for x in p["items"]] == sorted(nos, reverse=True), p)
    s, p = get(sort="name", dir="desc")
    check("sort name desc", s == 200 and letters(p) == ["D", "C", "B", "A"], (s, p))
    s, p = get(sort="bogus", dir="desc")
    check("unknown sort is ignored (default order)", s == 200 and letters(p) == ["A", "B", "C", "D"], (s, p))
    s, p = get(sort="joined", pageSize=2, page=2)
    check("paging after sort", s == 200 and p["total"] == 4 and letters(p) == ["D", "B"], (s, p))

    s, p = get(token=cb, campus=b, designation="Clerk")
    check("campus-B admin cannot see campus-A rows via designation", s == 200 and p["total"] == 0, (s, p))
    s, p = get(token=cb, campus=b, sort="joined")
    check("campus-B admin cannot see campus-A rows via sort", s == 200 and p["total"] == 0, (s, p))

    s, r = call("GET", "/staff/designations", token=ca, campus=a)
    expect = sorted({x["designation"] for x in _all_staff(ca, a)})
    check("designations: distinct, sorted, campus-visible", s == 200 and r["data"] == expect
          and {"Clerk", "Librarian", "Accountant"} <= set(r["data"]), (s, r, expect))
    s, r = call("GET", "/staff/designations", token=cb, campus=b)
    expect = sorted({x["designation"] for x in _all_staff(cb, b)})
    check("designations: other campus sees only its own", s == 200 and r["data"] == expect, (s, r, expect))
    _, te, tp = make_user(hq, campus_ids=[a], role_names=["Teacher"])
    teacher = login(te, tp)["tokens"]["accessToken"]
    s, _ = call("GET", "/staff/designations", token=teacher, campus=a)
    check("designations needs staff view permission", s == 403, s)


# ------------------------------------------------------------------ runner

def main(argv):
    names = argv or ["direct"]
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
            retire_staff(hq)
            cleanup(hq)
    return summary()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
