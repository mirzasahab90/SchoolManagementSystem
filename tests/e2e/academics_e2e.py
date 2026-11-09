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
        except AssertionError as e:
            check(f"{name}: setup", False, e)
        finally:
            cleanup(hq)
    return summary()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
