import os
import uuid
import pytest
import requests

# ===== Config =====
BASE_URL = os.getenv("BASE_URL", "http://localhost:8000")  
API_VERSION = os.getenv("API_VERSION", "v1").lower()      

def _suffix(n=6):
    return uuid.uuid4().hex[:n]
def build_register_payload(version: str, suffix: str):
    if version == "v1":
        return {
            "username": f"testuser_{suffix}",
            "password": "testpass123!",
            "name": "Test User",
            "email": f"test_{suffix}@example.com",
            "phoneNumber": f"0612{suffix}",
            "birthYear": 2000
            
        }
    else:
        return {
            "Username": f"testuser_{suffix}",
            "Password": "testpass123!",
            "Name": "Test User",
            "Email": f"test_{suffix}@example.com",
            "PhoneNumber": f"0612{suffix}",
            "BirthYear": 2000
        }

def build_login_payload(version: str, reg_payload: dict):
    if version == "v1":
        return {
            "username": reg_payload["username"],
            "password": reg_payload["password"]
        }
    else:
        return {
            "Username": reg_payload["Username"],
            "Password": reg_payload["Password"]
        }

@pytest.fixture(scope="session")
def base_url():
    return BASE_URL

@pytest.fixture(scope="session")
def api_version():
    return API_VERSION

@pytest.fixture(scope="session")
def shared_user_payload(api_version):
    s = _suffix()
    return build_register_payload(api_version, s)


def test_register(base_url, api_version):
    payload = build_register_payload(api_version, _suffix())
    r = requests.post(f"{base_url}/register", json=payload, timeout=5)
    if api_version == "v1":
        assert r.status_code == 201, f"v1: verwacht 201, kreeg {r.status_code}: {r.text}"
    else:
        assert r.status_code == 201, f"v2: verwacht 201, kreeg {r.status_code}: {r.text}"
        try:
            data = r.json()
            assert any(k.lower() == "username" for k in data.keys()), f"v2: unexpected body: {data}"
        except ValueError:
            pytest.fail(f"v2: expected JSON body, got: {r.text}")

def test_register_duplicate(base_url, api_version, shared_user_payload):
    r1 = requests.post(f"{base_url}/register", json=shared_user_payload, timeout=5)
    if api_version == "v1":
        assert r1.status_code in (201, 200), f"v1: verwacht 201/200 first, kreeg {r1.status_code}: {r1.text}"
    else:
        assert r1.status_code in (201, 409), f"v2: verwacht 201/409 first, kreeg {r1.status_code}: {r1.text}"

    # 2e call: 
    r2 = requests.post(f"{base_url}/register", json=shared_user_payload, timeout=5)
    if api_version == "v1":
        # veel v1-implementaties geven 200 + message "Username already taken"
        assert r2.status_code in (200, 409), f"v1: verwacht 200/409 duplicate, kreeg {r2.status_code}: {r2.text}"
    else:
        assert r2.status_code == 409, f"v2: verwacht 409 duplicate, kreeg {r2.status_code}: {r2.text}"

def test_login_success(base_url, api_version, shared_user_payload):
    requests.post(f"{base_url}/register", json=shared_user_payload, timeout=5)

    login_data = build_login_payload(api_version, shared_user_payload)
    r = requests.post(f"{base_url}/login", json=login_data, timeout=5)

    try:
        body = r.json()
    except ValueError:
        body = r.text

    assert r.status_code == 200, f"{api_version}: verwacht 200, kreeg {r.status_code}: {body}"

    if api_version == "v2":
        assert isinstance(body, dict) and "token" in body and "user" in body, f"v2: unexpected body {body}"

def test_login_wrong_password(base_url, api_version, shared_user_payload):
    requests.post(f"{base_url}/register", json=shared_user_payload, timeout=5)

    bad = build_login_payload(api_version, shared_user_payload)
    if api_version == "v1":
        bad["password"] = "wrongpassword"
    else:
        bad["Password"] = "wrongpassword"

    r = requests.post(f"{base_url}/login", json=bad, timeout=5)
    assert r.status_code == 401, f"{api_version}: verwacht 401, kreeg {r.status_code}: {r.text}"
