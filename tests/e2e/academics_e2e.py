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
