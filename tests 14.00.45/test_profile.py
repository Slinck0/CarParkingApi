import pytest
import requests
import uuid
import time
import random
import os

BASE_URL = os.getenv("BASE_URL", "http://localhost:5000")

def _unique_suffix():
    return f"{int(time.time())}_{uuid.uuid4().hex[:6]}"

@pytest.fixture
def base_url():
    return BASE_URL

@pytest.fixture(scope="function")
def user_data():
    suffix = _unique_suffix()
    return {
        "username": f"user_{suffix}",
        "password": "Password123!",
        "name": f"Test User {suffix}",
        "email": f"test_{suffix}@example.com",
        "phoneNumber": f"06{random.randint(10000000, 99999999)}",
        "birthYear": 2000
    }

@pytest.fixture
def auth_token(base_url, user_data):
    reg_resp = requests.post(f"{base_url}/register", json=user_data, timeout=5)
    assert reg_resp.status_code in (200, 201), f"Register failed: {reg_resp.text}"

    login_payload = {
        "username": user_data["username"],
        "password": user_data["password"]
    }
    
    r = requests.post(f"{base_url}/login", json=login_payload, timeout=5)
    assert r.status_code == 200, f"Login failed: {r.status_code} {r.text}"
    
    data = r.json()
    token = data.get("token")
    assert token, f"No 'token' in response: {r.text}"
    
    return token

@pytest.fixture
def auth_header(auth_token):
    return {"Authorization": f"Bearer {auth_token}"}

def test_get_profile_ok(base_url, auth_header, user_data):
    r = requests.get(f"{base_url}/profile", headers=auth_header, timeout=5)
    
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    body = r.json()
    
    assert body.get("username") == user_data["username"]
    assert body.get("email") == user_data["email"]

def test_get_profile_unauthenticated(base_url):
    r = requests.get(f"{base_url}/profile", timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"

def test_update_profile_ok(base_url, auth_header):
    suffix = _unique_suffix()
    update_payload = {
        "name": "Renamed User", 
        "email": f"new_{suffix}@test.com",
        "phoneNumber": f"06{random.randint(10000000, 99999999)}",
        "birthYear": 1999
    }

    r = requests.put(f"{base_url}/profile",
                     json=update_payload,
                     headers=auth_header,
                     timeout=5)
    
    assert r.status_code == 200, f"Update failed: {r.status_code} {r.text}"

    r_get = requests.get(f"{base_url}/profile", headers=auth_header, timeout=5)
    body = r_get.json()
    assert body.get("name") == "Renamed User"
    assert body.get("email") == update_payload["email"]

def test_update_profile_bad_request(base_url, auth_header):
    bad_payload = {
        "name": "Bad User",
        "email": "geen-email-adres",
        "phoneNumber": "123",
        "birthYear": 2000
    }

    r = requests.put(f"{base_url}/profile",
                     json=bad_payload,
                     headers=auth_header,
                     timeout=5)
    
    assert r.status_code == 400, f"Expected 400 Bad Request, got {r.status_code}"

def test_update_profile_unauthenticated(base_url):
    r = requests.put(f"{base_url}/profile",
                     json={"name": "Hacker"},
                     timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"