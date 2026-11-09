"""End-to-end checks for campus isolation, Campus Admin and academic setup.

Usage: python tests/e2e/academics_e2e.py <group> [<group> ...]
Groups run against the local dev API and clean up everything they create.
"""
import sys

from sms_client import (
    admin_credentials, admin_token, call, check, cleanup, data, demo_campuses, login,
    make_user, role_id, summary, temp_password, track, user_payload, wait_for_api,
)

GROUPS = {}


def group(fn):
    GROUPS[fn.__name__.replace("_", "-")] = fn
    return fn


def full_matrix(keys, view=True, add=True, edit=True, delete=True):
    return [{"moduleKey": k, "moduleName": k, "view": view, "add": add, "edit": edit, "delete": delete} for k in keys]


def menu_keys(user):
    return [m["key"] for m in user["menu"]]


# ------------------------------------------------------------------ isolation (ported, previous feature)

@group
def isolation(hq):
    a, b = demo_campuses(hq)
    s, r = call("POST", "/auth/login", dict(zip(("email", "password"), admin_credentials())))
    check("HQ defaults to All campuses", r["data"]["user"].get("activeCampusId") is None, r["data"]["user"])
    campuses = data(call("GET", "/campuses", token=hq))
    check("HQ sees all campuses", len(campuses) >= 2, campuses)

    s, r = call("POST", "/roles", {"name": "QA Iso Admin", "description": "temp"}, token=hq)
    check("create test role", s == 200, (s, r))
    rid = track("roles", r["data"]["id"])
    matrix = full_matrix(["dashboard", "campuses", "users", "roles"])
    s, r = call("PUT", f"/roles/{rid}", {"name": "QA Iso Admin", "description": "temp", "isActive": True, "matrix": matrix}, token=hq)
    check("grant test role permissions", s == 200, (s, r))

    uid, email, pw = make_user(hq, campus_ids=[a], role_ids=[rid])
    auth = login(email, pw)
    t = auth["tokens"]["accessToken"]
    check("campus user defaults to own campus", auth["user"]["activeCampusId"] == a, auth["user"]["activeCampusId"])
    s, r = call("GET", "/campuses", token=t, campus=b)
    check("forged X-Campus-Id -> 403", s == 403, (s, r))
    s, r = call("GET", "/auth/me", token=t, campus=b)
    check("/auth/me with forged campus falls back", s == 200 and r["data"]["activeCampusId"] == a, (s, r))
    s, r = call("GET", "/campuses", token=t, campus=a)
    check("campus user sees only own campus", s == 200 and [c["id"] for c in r["data"]] == [a], (s, r))
    s, r = call("GET", f"/campuses/{b}", token=t, campus=a)
    check("campus user cannot read campus B", s == 404, (s, r))
    s, r = call("POST", "/campuses", {"code": "QAX", "name": "QA X", "isActive": True}, token=t, campus=a)
    check("campus user cannot create campus", s == 403, (s, r))
    s, r = call("GET", "/users", token=t, campus=a)
    emails = [u["email"] for u in r["data"]]
    check("campus user list hides HQ users", s == 200 and admin_credentials()[0] not in emails, emails)
    me = user_payload(campus_ids=[a], role_ids=[rid], email=email, password="")
    me["password"] = None
    s, r = call("PUT", f"/users/{uid}", dict(me, isHq=True), token=t, campus=a)
    check("cannot make self HQ", s == 403, (s, r))
    s, r = call("PUT", f"/users/{uid}", dict(me, campusIds=[a, b]), token=t, campus=a)
    check("cannot add campus B to self", s == 403, (s, r))
    s, r = call("POST", "/users", user_payload(campus_ids=[b], role_ids=[rid]), token=t, campus=a)
    check("cannot create user in campus B", s == 403, (s, r))
    admin_id = data(call("GET", "/auth/me", token=hq))["id"]
    s, r = call("DELETE", f"/users/{admin_id}", token=t, campus=a)
    check("cannot delete HQ admin", s in (403, 404), (s, r))
    s, r = call("PUT", f"/roles/{rid}", {"name": "QA Iso Admin", "isActive": True, "matrix": matrix}, token=t, campus=a)
    check("cannot edit an HQ role", s == 403, (s, r))
    s, r = call("GET", "/dashboard", token=t, campus=a)
    campuses_kpi = next(k for k in r["data"]["kpis"] if k["id"] == "campuses")
    check("dashboard scoped to campus", s == 200 and campuses_kpi["value"] == "1", (s, r))
    small = [m for m in matrix if m["moduleKey"] != "users"]
    call("PUT", f"/roles/{rid}", {"name": "QA Iso Admin", "isActive": True, "matrix": small}, token=hq)
    s, r = call("GET", "/users", token=t, campus=a)
    check("revoked permission takes effect immediately", s == 403, (s, r))
    s, r = call("PUT", f"/users/{uid}", dict(me, isActive=False), token=hq)
    check("admin deactivates campus user", s == 200, (s, r))
    s, r = call("GET", "/dashboard", token=t, campus=a)
    check("deactivated user's token rejected", s == 401, (s, r))


# ------------------------------------------------------------------ schema

@group
def schema(hq):
    s, r = call("GET", "/roles", token=hq)
    check("roles list exposes ownership (hqAssignOnly)", s == 200 and all("hqAssignOnly" in x for x in r["data"]), r)
    s, r = call("GET", "/academic-sessions", token=hq)
    check("academic-sessions endpoint exists", s == 200, (s, r))


# ------------------------------------------------------------------ seed

def role_detail(token, name):
    return data(call("GET", f"/roles/{role_id(token, name)}", token=token))


def actions(detail, key):
    cell = next((c for c in detail["matrix"] if c["moduleKey"] == key), None)
    return {a for a in ("view", "add", "edit", "delete") if cell and cell[a]}


@group
def seed(hq):
    a, _ = demo_campuses(hq)
    me = data(call("GET", "/auth/me", token=hq))
    check("HQ All-campuses top menu order", menu_keys(me) == ["dashboard", "campuses", "academics", "settings"], menu_keys(me))
    academics = next((m for m in me["menu"] if m["key"] == "academics"), None)
    check("academics children", academics and [c["key"] for c in academics["children"]]
          == ["academic-sessions", "classes", "subjects", "sections"], academics)
    me_a = data(call("GET", "/auth/me", token=hq, campus=a))
    check("my-campus shown at position 2 with a campus selected", menu_keys(me_a)[:2] == ["dashboard", "my-campus"], menu_keys(me_a))

    roles = data(call("GET", "/roles", token=hq))
    ca = next((r for r in roles if r["name"] == "CampusAdmin"), None)
    check("CampusAdmin role is HQ-owned and HQ-assign-only", ca and ca["hqAssignOnly"] and ca.get("campusId") is None, ca)
    if ca:
        d = role_detail(hq, "CampusAdmin")
        full = {"view", "add", "edit", "delete"}
        check("CampusAdmin full on users/roles/sections/my-campus",
              all(actions(d, k) == full for k in ["users", "roles", "sections", "my-campus"]),
              {k: actions(d, k) for k in ["users", "roles", "sections", "my-campus"]})
        check("CampusAdmin view+edit on campuses", actions(d, "campuses") == {"view", "edit"}, actions(d, "campuses"))
    teacher = role_detail(hq, "Teacher")
    check("Teacher can view sections", "view" in actions(teacher, "sections"), actions(teacher, "sections"))
    sa = next(r for r in roles if r["name"] == "SuperAdmin")
    check("SuperAdmin is HQ-assign-only", sa["hqAssignOnly"], sa)

    s, r = call("GET", "/academic-sessions", token=hq)
    items = r["data"]["items"] if s == 200 else []
    current = next((x for x in items if x["name"] == "2026-27"), None)
    check("seeded HQ current session 2026-27", current and current["isCurrent"] and current.get("campusId") is None
          and r["data"].get("effectiveCurrentId") == current["id"], (s, r))


# ------------------------------------------------------------------ password

@group
def password(hq):
    a, _ = demo_campuses(hq)
    uid, email, p1 = make_user(hq, campus_ids=[a], role_names=["Teacher"], must_change=True)
    auth = login(email, p1)
    check("new user must change password", auth["user"]["mustChangePassword"] is True, auth["user"])
    t = auth["tokens"]["accessToken"]
    p2 = temp_password()
    s, r = call("POST", "/auth/change-password", {"currentPassword": "wrong-" + p1, "newPassword": p2}, token=t)
    check("wrong current password -> 400", s == 400, (s, r))
    s, r = call("POST", "/auth/change-password", {"currentPassword": p1, "newPassword": "Ab#1234"}, token=t)
    check("7-character new password -> 400", s == 400, (s, r))
    s, r = call("POST", "/auth/change-password", {"currentPassword": p1, "newPassword": p1}, token=t)
    check("new equal to current -> 400", s == 400, (s, r))
    s, r = call("POST", "/auth/change-password", {"currentPassword": p1, "newPassword": p2}, token=t)
    check("change password -> 200", s == 200, (s, r))
    me = data(call("GET", "/auth/me", token=t))
    check("flag cleared after change", me and me["mustChangePassword"] is False, me)
    s, _ = call("POST", "/auth/login", {"email": email, "password": p1})
    check("old password stops working", s == 401, s)
    s, _ = call("POST", "/auth/login", {"email": email, "password": p2})
    check("new password works", s == 200, s)


# ------------------------------------------------------------------ roles-ownership

def campus_admin(hq, campus_id):
    """A temporary CampusAdmin for one campus; returns (user_id, token)."""
    uid, email, pw = make_user(hq, campus_ids=[campus_id], role_names=["CampusAdmin"])
    return uid, login(email, pw)["tokens"]["accessToken"]


def create_role(token, name, campus=None, campus_id=None):
    body = {"name": name, "description": "temp"}
    if campus_id is not None:
        body["campusId"] = campus_id
    s, r = call("POST", "/roles", body, token=token, campus=campus)
    if s == 200:
        track("roles", r["data"]["id"])
    return s, r


@group
def roles_ownership(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)

    s, r = create_role(ca, "QA Coach", campus=a)
    check("campus admin creates a campus role", s == 200 and r["data"].get("campusCode"), (s, r))
    coach = r["data"]["id"] if s == 200 else None
    names_b = [x["name"] for x in data(call("GET", "/roles", token=hq, campus=b))]
    check("campus role hidden on another campus", "QA Coach" not in names_b, names_b)
    names_all = [x["name"] for x in data(call("GET", "/roles", token=hq))]
    check("HQ on All campuses sees campus role", "QA Coach" in names_all, names_all)

    teacher = role_id(hq, "Teacher")
    s, r = call("PUT", f"/roles/{teacher}", {"name": "Teacher", "isActive": True, "matrix": full_matrix(["sections"])}, token=ca, campus=a)
    check("campus admin cannot edit an HQ role", s == 403, (s, r))

    if coach:
        s, r = call("PUT", f"/roles/{coach}", {"name": "QA Coach", "isActive": True,
                                               "matrix": full_matrix(["campuses"])}, token=ca, campus=a)
        check("cannot grant unheld permission (campuses.delete)", s == 403 and "only grant permissions you have" in str(r), (s, r))
        s, r = call("PUT", f"/roles/{coach}", {"name": "QA Coach", "isActive": True,
                                               "matrix": full_matrix(["sections"], edit=False, delete=False)}, token=ca, campus=a)
        check("can grant held permissions", s == 200, (s, r))

        s, r = call("POST", "/users", user_payload(campus_ids=[a], role_ids=[coach]), token=ca, campus=a)
        check("campus role assignable to its campus users", s == 200, (s, r))
        if s == 200:
            track("users", r["data"]["id"])
        s, r = call("POST", "/users", user_payload(campus_ids=[a, b], role_ids=[coach]), token=hq)
        check("campus role refused for user also on another campus", s == 403, (s, r))
        if s == 200:
            track("users", r["data"]["id"])

    ca_role = role_id(hq, "CampusAdmin")
    s, r = call("POST", "/users", user_payload(campus_ids=[a], role_ids=[ca_role]), token=ca, campus=a)
    check("campus admin cannot assign CampusAdmin", s == 403 and "Only HQ can assign this role." in str(r), (s, r))
    if s == 200:
        track("users", r["data"]["id"])
    assignable = [x["name"] for x in data(call("GET", "/roles?assignable=true", token=ca, campus=a))]
    check("HQ-only roles not assignable by campus admin", "SuperAdmin" not in assignable and "CampusAdmin" not in assignable
          and "Teacher" in assignable, assignable)

    s, r = create_role(ca, "Teacher", campus=a)
    check("campus role cannot shadow an HQ role name", s == 409, (s, r))


# ------------------------------------------------------------------ campus-admin

def campus_body(code, admin=None, **extra):
    body = {"code": code, "name": f"QA {code}", "city": "Karachi", "isActive": True}
    body.update(extra)
    if admin:
        body["admin"] = admin
    return body


@group
def campus_admin_flow(hq):
    a, _ = demo_campuses(hq)
    pw = temp_password()
    email = f"qa.ca.{temp_password()[4:10].lower()}@example.test".replace("-", "x").replace("_", "x")
    s, r = call("POST", "/campuses", campus_body("QAC", {"firstName": "Qa", "lastName": "Head", "email": email, "password": pw}), token=hq)
    ok = check("create campus with admin", s == 200 and len(r["data"].get("admins", [])) == 1
               and r["data"]["admins"][0]["email"] == email, (s, r))
    if not ok:
        return
    qac = track("campuses", r["data"]["id"])
    admin_id = r["data"]["admins"][0]["id"]
    track("users", admin_id)

    auth = login(email, pw)
    u = auth["user"]
    check("admin must change password on first sign-in", u["mustChangePassword"] is True, u)
    check("admin works only in the new campus", [c["id"] for c in u["campuses"]] == [qac] and u["activeCampusId"] == qac, u["campuses"])
    keys = menu_keys(u)
    check("admin menu has My campus, no Campuses", "my-campus" in keys and "campuses" not in keys, keys)
    t = auth["tokens"]["accessToken"]
    pw2 = temp_password()
    call("POST", "/auth/change-password", {"currentPassword": pw, "newPassword": pw2}, token=t)

    s, r = call("GET", f"/campuses/{qac}", token=t)
    check("admin reads own campus", s == 200, (s, r))
    campus = r["data"]
    profile = {k: campus.get(k) for k in ["code", "name", "address", "city", "state", "country", "postalCode", "phone", "email",
                                          "website", "inChargeName", "inChargePhone", "inChargeEmail", "inChargeDesignation",
                                          "establishedOn", "studentCapacity", "notes", "isActive"]}
    s, r = call("PUT", f"/campuses/{qac}", dict(profile, name="QA Renamed"), token=t)
    check("admin edits own campus profile", s == 200 and r["data"]["name"] == "QA Renamed", (s, r))
    s, r = call("PUT", f"/campuses/{qac}", dict(profile, code="QAZ"), token=t)
    check("admin cannot change campus code", s == 403, (s, r))
    s, r = call("GET", f"/campuses/{a}", token=t)
    check("admin cannot read another campus", s == 404, (s, r))

    teacher = role_id(hq, "Teacher")
    s, r = call("POST", "/users", user_payload(campus_ids=[qac], role_ids=[teacher]), token=t)
    check("admin creates a Teacher", s == 200, (s, r))
    if s == 200:
        track("users", r["data"]["id"])
    s, r = call("POST", "/users", user_payload(campus_ids=[qac], role_ids=[role_id(hq, "CampusAdmin")]), token=t)
    check("admin cannot create another CampusAdmin", s == 403, (s, r))
    s, r = call("POST", "/users", user_payload(campus_ids=[], role_ids=[teacher], hq=True), token=t)
    check("admin cannot create an HQ user", s == 403, (s, r))

    s, r = call("POST", "/campuses", campus_body("QAD", {"firstName": "Qa", "lastName": "Dup", "email": email, "password": temp_password()}), token=hq)
    check("duplicate admin email -> 409", s == 409, (s, r))
    if s == 200:
        track("campuses", r["data"]["id"])
    codes = [c["code"] for c in data(call("GET", "/campuses", token=hq))]
    check("failed create leaves no campus behind", "QAD" not in codes, codes)


GROUPS["campus-admin"] = GROUPS.pop("campus-admin-flow")


# ------------------------------------------------------------------ sessions

def session_body(name, start, end, status="Planned", campus_id=None):
    body = {"name": name, "startDate": start, "endDate": end, "status": status}
    if campus_id is not None:
        body["campusId"] = campus_id
    return body


def create_session(token, body, campus=None):
    s, r = call("POST", "/academic-sessions", body, token=token, campus=campus)
    if s == 200:
        track("academic-sessions", r["data"]["id"])
    return s, r


def sessions_list(token, campus=None):
    return data(call("GET", "/academic-sessions", token=token, campus=campus))


def hq_current_id(token):
    return next(x["id"] for x in sessions_list(token)["items"] if x.get("campusId") is None and x["isCurrent"])


@group
def sessions(hq):
    a, b = demo_campuses(hq)
    _, ca = campus_admin(hq, a)
    hq_current = hq_current_id(hq)

    s, r = create_session(hq, session_body("QA-2099", "2099-04-01", "2100-03-31"))
    check("HQ creates HQ session", s == 200 and r["data"].get("campusId") is None, (s, r))
    qa = r["data"]["id"] if s == 200 else None
    s, r = create_session(hq, session_body("QA-2099", "2101-04-01", "2102-03-31"))
    check("duplicate session name -> 409", s == 409, (s, r))
    s, r = create_session(hq, session_body("QA-OVL", "2099-06-01", "2099-12-31"))
    check("overlapping HQ session -> 400", s == 400, (s, r))
    s, r = create_session(hq, session_body("QA-BAD", "2105-04-01", "2105-01-01"))
    check("end before start -> 400", s == 400, (s, r))
    if qa:
        s, r = call("PUT", f"/academic-sessions/{qa}", session_body("QA-2099", "2099-04-01", "2100-03-31", "Active"), token=hq)
        check("edit keeps own dates (no self-overlap)", s == 200, (s, r))

    s, r = create_session(ca, session_body("QA-A", "2099-04-01", "2100-03-31"), campus=a)
    check("campus session may share HQ dates", s == 200 and r["data"].get("campusCode"), (s, r))
    qa_a = r["data"]["id"] if s == 200 else None
    if qa_a:
        s, r = call("POST", f"/academic-sessions/{qa_a}/set-current", token=ca, campus=a)
        check("campus sets its own current session", s == 200, (s, r))
        check("campus A effective current is its own", sessions_list(ca, a)["effectiveCurrentId"] == qa_a)
        check("campus B effective current is still HQ", sessions_list(hq, b)["effectiveCurrentId"] == hq_current)
        hq_items = sessions_list(hq)["items"]
        check("HQ current session unchanged", next(x for x in hq_items if x["id"] == hq_current)["isCurrent"], hq_items)
        names_b = [x["name"] for x in sessions_list(hq, b)["items"]]
        check("campus session hidden on another campus", "QA-A" not in names_b, names_b)
        s, r = call("DELETE", f"/academic-sessions/{qa_a}", token=ca, campus=a)
        check("cannot delete a current session", s == 400, (s, r))
        s, r = call("POST", f"/academic-sessions/{hq_current}/set-current", token=ca, campus=a)
        check("campus picks HQ session again -> override cleared", s == 200
              and sessions_list(ca, a)["effectiveCurrentId"] == hq_current, (s, r))
        check("HQ session still current after campus reset",
              next(x for x in sessions_list(hq)["items"] if x["id"] == hq_current)["isCurrent"])

    if qa:
        call("PUT", f"/academic-sessions/{qa}", session_body("QA-2099", "2099-04-01", "2100-03-31", "Closed"), token=hq)
        s, r = call("POST", f"/academic-sessions/{qa}/set-current", token=hq)
        check("set-current on Closed -> 400", s == 400 and "Reopen the session first." in str(r), (s, r))

    s, r = call("PUT", f"/academic-sessions/{hq_current}", session_body("2026-27", "2026-04-01", "2027-03-31", "Active"), token=ca, campus=a)
    check("campus admin cannot change HQ session", s == 403, (s, r))
    s, r = call("DELETE", f"/academic-sessions/{hq_current}", token=hq)
    check("cannot delete HQ current session", s == 400, (s, r))


# ------------------------------------------------------------------ runner

def main(argv):
    names = argv or ["isolation"]
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
