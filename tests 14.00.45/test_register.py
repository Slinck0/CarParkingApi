import pytest
import requests
import uuid
import time
import random
import os

BASE_URL = os.getenv("BASE_URL", "http://localhost:5000")

def _unique_suffix():
    return f"{int(time.time())}_{uuid.uuid4().hex[:6]}"

def _random_phone():
    return f"06{random.randint(10000000, 99999999)}"

@pytest.fixture
def base_url():
    return BASE_URL

def build_user_payload():
    suffix = _unique_suffix()
    return {
        "username": f"user_{suffix}",
        "password": "Password123!",
        "name": f"Test User {suffix}",
        "email": f"test_{suffix}@example.com",
        "phoneNumber": _random_phone(),
        "birthYear": 2000
    }

def test_register_success(base_url):
    payload = build_user_payload()
    r = requests.post(f"{base_url}/register", json=payload, timeout=5)
    
    assert r.status_code in (200, 201), f"Expected 201/200, got {r.status_code}: {r.text}"
    
    try:
        data = r.json()
        keys_lower = [k.lower() for k in data.keys()]
        assert "username" in keys_lower or "id" in keys_lower, f"Unexpected body: {data}"
    except ValueError:
        pytest.fail(f"Expected JSON response, got: {r.text}")

def test_register_duplicate_conflict(base_url):
    payload = build_user_payload()
    
    r1 = requests.post(f"{base_url}/register", json=payload, timeout=5)
    assert r1.status_code in (200, 201), f"First register failed: {r1.text}"

    r2 = requests.post(f"{base_url}/register", json=payload, timeout=5)
    assert r2.status_code == 409, f"Expected 409 for duplicate, got {r2.status_code}: {r2.text}"

def test_login_success(base_url):
    payload = build_user_payload()
    requests.post(f"{base_url}/register", json=payload, timeout=5)

    login_payload = {
        "username": payload["username"],
        "password": payload["password"]
    }

    r = requests.post(f"{base_url}/login", json=login_payload, timeout=5)
    assert r.status_code == 200, f"Login failed: {r.status_code} {r.text}"
    
    body = r.json()
    assert "token" in body, f"No token in response: {body}"

def test_login_wrong_password(base_url):
    payload = build_user_payload()
    requests.post(f"{base_url}/register", json=payload, timeout=5)

    bad_payload = {
        "username": payload["username"],
        "password": "WrongPassword!"
    }

    r = requests.post(f"{base_url}/login", json=bad_payload, timeout=5)
    assert r.status_code == 401, f"Expected 401, got {r.status_code}: {r.text}"