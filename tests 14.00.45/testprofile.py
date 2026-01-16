# test_profile.py
import pytest
import requests

@pytest.fixture
def base_url():
    return "http://localhost:8000"

@pytest.fixture
def creds():
    return {"username": "testuser", "password": "testpass123"}

@pytest.fixture
def auth_token(base_url, creds):
    # register, ignore if exists.
    requests.post(f"{base_url}/register",
                  json={"name": "Test User", **creds},
                  timeout=5)
    # login
    r = requests.post(f"{base_url}/login", json=creds, timeout=5)
    assert r.status_code in (200, 201), f"Login failed: {r.status_code} {r.text}"
    #save session token
    token = r.json().get("session_token")
    assert token, f"No session_token in response: {r.text}"
    return token

def json_or_text(resp: requests.Response):
    try:
        return resp.json()
    except ValueError:
        return {"_raw": resp.text}

def test_get_profile_ok(base_url, auth_token, creds):
    r = requests.get(f"{base_url}/profile",
                     headers={"Authorization": auth_token},
                     timeout=5)
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    body = r.json()
    assert body.get("username") == creds["username"]

def test_get_profile_unauthenticated(base_url):
    r = requests.get(f"{base_url}/profile", timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"

@pytest.fixture
def profile_payload_ok():
    # Updated user values
    return {"name": "Renamed User", "password": "newpass456"}

def test_update_profile_ok(base_url, auth_token, profile_payload_ok):
    r = requests.put(f"{base_url}/profile",
                     json=profile_payload_ok,
                     headers={"Authorization": auth_token},
                     timeout=5)
    assert r.status_code in (200, 204), f"{r.status_code} {r.text}"
    # Check if the server returns the expected text
    assert "updated" in r.text.lower()

def test_update_profile_missing_password_key_causes_server_error(base_url, auth_token):
    r = requests.put(f"{base_url}/profile",
                     json={"name": "No Password Key"},
                     headers={"Authorization": auth_token},
                     timeout=5)
    assert 500 <= r.status_code < 600, \
        f"Expected {r.status_code} {r.text} due to missing password"

def test_update_profile_empty_password_allowed(base_url, auth_token):
    # Empty string is does nothing but will return a successful status code
    r = requests.put(f"{base_url}/profile",
                     json={"name": "Keep Password", "password": ""},
                     headers={"Authorization": auth_token},
                     timeout=5)
    assert r.status_code in (200, 204), f"{r.status_code} {r.text}"

def test_update_profile_unauthenticated(base_url):
    # No auth, 401 expected
    r = requests.put(f"{base_url}/profile",
                     json={"name": "X", "password": "y"},
                     timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"
