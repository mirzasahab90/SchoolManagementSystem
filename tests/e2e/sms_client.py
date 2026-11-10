"""Tiny HTTP client + helpers for the local-dev end-to-end checks.

Talks only to the local dev API (env SMS_API, default https://localhost:7150/api).
TLS verification is disabled for localhost only. The seeded admin login is read
from DatabaseSeeder.cs so no credential lives in this file.
"""
import json
import os
import re
import secrets
import ssl
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import urlparse

BASE = os.environ.get("SMS_API", "https://localhost:7150/api").rstrip("/")
_host = urlparse(BASE).hostname
if _host not in ("localhost", "127.0.0.1"):
    sys.exit("Refusing to run end-to-end checks against a non-local API: " + BASE)

_CTX = ssl._create_unverified_context()  # localhost dev certificate only
_ROOT = Path(__file__).resolve().parents[2]
_SEED = _ROOT / "SchoolManagementSystem-API" / "SchoolManagementSystem.Infrastructure" / "Seeding" / "DatabaseSeeder.cs"


def call(method, path, body=None, token=None, campus=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    if campus is not None:
        req.add_header("X-Campus-Id", str(campus))
    try:
        with urllib.request.urlopen(req, context=_CTX) as r:
            raw = r.read()
            return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw.decode(errors="replace")


def wait_for_api(seconds=60):
    for _ in range(seconds):
        try:
            urllib.request.urlopen(BASE.rsplit("/api", 1)[0] + "/", context=_CTX)
            return
        except Exception:
            time.sleep(1)
    sys.exit("API did not come up at " + BASE)


def admin_credentials():
    src = _SEED.read_text(encoding="utf-8")
    email = re.search(r'AdminEmail = "([^"]+)"', src).group(1)
    password = re.search(r'AdminPassword = "([^"]+)"', src).group(1)
    return email, password


def login(email, password):
    status, body = call("POST", "/auth/login", {"email": email, "password": password})
    if status != 200:
        raise AssertionError(f"login failed for {email}: {status} {body}")
    return body["data"]


def admin_token():
    return login(*admin_credentials())["tokens"]["accessToken"]


def temp_password():
    return "Qa#" + secrets.token_urlsafe(12)


def data(resp):
    status, body = resp
    return body["data"] if isinstance(body, dict) else None


# ---------------------------------------------------------------- results

_results = []


def check(name, cond, detail=""):
    _results.append((name, bool(cond)))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -> {detail}"))
    return bool(cond)


def summary():
    failed = [n for n, ok in _results if not ok]
    print(f"\n{len(_results) - len(failed)}/{len(_results)} passed")
    return 1 if failed else 0


# ---------------------------------------------------------------- cleanup

_created = {"exams": [], "holidays": [], "staff": [], "candidates": [], "vacancies": [], "students": [], "sections": [], "users": [], "roles": [], "classes": [], "subjects": [], "academic-sessions": [], "campuses": []}


def track(kind, ident):
    _created[kind].append(ident)
    return ident


def cleanup(token):
    """Delete tracked rows in dependency order (sections first, campuses last)."""
    for kind in ["exams", "holidays", "staff", "candidates", "vacancies", "students", "sections", "users", "roles", "classes", "subjects", "academic-sessions", "campuses"]:
        for ident in reversed(_created[kind]):
            route = "calendar/holidays" if kind == "holidays" else kind
            status, _ = call("DELETE", f"/{route}/{ident}", token=token)
            if status not in (200, 404):
                print(f"  cleanup: DELETE /{kind}/{ident} -> {status}")
        _created[kind].clear()


# ---------------------------------------------------------------- users & roles

def role_id(token, name, campus=None):
    roles = data(call("GET", "/roles", token=token, campus=campus)) or []
    for r in roles:
        if r["name"] == name:
            return r["id"]
    raise AssertionError(f"role {name} not found")


def user_payload(*, campus_ids, role_ids, hq=False, must_change=False, email=None, password=None):
    return {
        "firstName": "Qa", "lastName": "User", "email": email or f"qa.{secrets.token_hex(4)}@example.test",
        "phone": None, "isActive": True, "isHq": hq, "roleIds": role_ids, "campusIds": campus_ids,
        "password": password or temp_password(), "mustChangePassword": must_change,
    }


def make_user(token, *, campus_ids, role_names=None, role_ids=None, hq=False, must_change=False, campus=None):
    ids = list(role_ids or []) + [role_id(token, n, campus) for n in (role_names or [])]
    payload = user_payload(campus_ids=campus_ids, role_ids=ids, hq=hq, must_change=must_change)
    email, password = payload["email"], payload["password"]
    status, body = call("POST", "/users", payload, token=token, campus=campus)
    if status != 200:
        raise AssertionError(f"make_user failed: {status} {body}")
    track("users", body["data"]["id"])
    return body["data"]["id"], email, password


def demo_campuses(token):
    campuses = data(call("GET", "/campuses", token=token)) or []
    campuses.sort(key=lambda c: c["id"])
    return campuses[0]["id"], campuses[1]["id"]


# ---------------------------------------------------------------- files

def upload(path, fields, file_field, filename, content, content_type, token, campus=None):
    """POST a multipart/form-data body built by hand (stdlib only)."""
    boundary = "----smsboundary" + secrets.token_hex(8)
    parts = []
    for name, value in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    parts.append((f'--{boundary}\r\nContent-Disposition: form-data; name="{file_field}"; filename="{filename}"\r\n'
                  f"Content-Type: {content_type}\r\n\r\n").encode() + content + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    req = urllib.request.Request(BASE + path, data=b"".join(parts), method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    req.add_header("Authorization", "Bearer " + token)
    if campus is not None:
        req.add_header("X-Campus-Id", str(campus))
    try:
        with urllib.request.urlopen(req, context=_CTX) as r:
            raw = r.read()
            return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw.decode(errors="replace")


def download(path, token, campus=None):
    """GET raw bytes; returns (status, bytes, headers)."""
    req = urllib.request.Request(BASE + path, method="GET")
    req.add_header("Authorization", "Bearer " + token)
    if campus is not None:
        req.add_header("X-Campus-Id", str(campus))
    try:
        with urllib.request.urlopen(req, context=_CTX) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)
