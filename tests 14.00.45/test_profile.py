<<<<<<< HEAD
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
=======
import pytest
import requests
import uuid
import os

# Haal URL uit environment of gebruik default poort 5000 (standaard voor .NET)
BASE_URL = os.getenv("BASE_URL", "http://localhost:5000")

def _unique_suffix():
    return uuid.uuid4().hex[:8]

@pytest.fixture
def base_url():
    return BASE_URL

@pytest.fixture
def user_data():
    """Genereert unieke gebruikersdata voor elke testrun"""
    suffix = _unique_suffix()
    return {
        "username": f"user_{suffix}",
        "password": "Password123!",
        "name": f"Test User {suffix}",
        "email": f"test_{suffix}@example.com",
        "phoneNumber": "0612345678",
        "birthYear": 2000
    }

@pytest.fixture
def auth_token(base_url, user_data):
    # 1. Register (zodat de user bestaat)
    # We gebruiken de user_data die overeenkomt met jouw C# RegisterUserRequest
    reg_resp = requests.post(f"{base_url}/register", json=user_data, timeout=5)
    
    # Als de user al bestaat (409), proberen we in te loggen. 
    # Anders moet het 201 Created zijn.
    assert reg_resp.status_code in (201, 409), f"Register failed: {reg_resp.text}"

    # 2. Login
    login_payload = {
        "username": user_data["username"],
        "password": user_data["password"]
    }
    
    r = requests.post(f"{base_url}/login", json=login_payload, timeout=5)
    assert r.status_code == 200, f"Login failed: {r.status_code} {r.text}"
    
    # 3. Save token
    # Jouw C# API geeft { "token": "...", "user": {...} } terug
    data = r.json()
    token = data.get("token")
    assert token, f"No 'token' in response: {r.text}"
    
    return token

def test_get_profile_ok(base_url, auth_token, user_data):
    # Let op: .NET verwacht vaak 'Bearer <token>'
    headers = {"Authorization": f"Bearer {auth_token}"}
    
    r = requests.get(f"{base_url}/profile", headers=headers, timeout=5)
    
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    body = r.json()
    
    # Controleer of de data overeenkomt met wat we geregistreerd hebben
    assert body.get("username") == user_data["username"]
    assert body.get("email") == user_data["email"]
>>>>>>> origin/Rens-new

def test_get_profile_unauthenticated(base_url):
    r = requests.get(f"{base_url}/profile", timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"

<<<<<<< HEAD
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
=======
def test_update_profile_ok(base_url, auth_token):
    headers = {"Authorization": f"Bearer {auth_token}"}
    
    # Nieuwe data conform jouw C# UpdateProfileRequest(Name, Email, PhoneNumber, BirthYear)
    # Let op: Wachtwoord updaten via dit endpoint werd niet ondersteund in je C# code.
    new_email = f"updated_{_unique_suffix()}@example.com"
    payload = {
        "name": "Renamed User",
        "email": new_email,
        "phoneNumber": "0698765432",
        "birthYear": 1995
    }
    
    r = requests.put(f"{base_url}/profile", json=payload, headers=headers, timeout=5)
    
    assert r.status_code == 200, f"Update failed: {r.status_code} {r.text}"
    
    # Verifieer de update door profiel opnieuw op te halen
    body = r.json()
    assert body.get("name") == "Renamed User"
    assert body.get("email") == new_email
    assert body.get("birthYear") == 1995

def test_update_profile_missing_fields_causes_bad_request(base_url, auth_token):
    headers = {"Authorization": f"Bearer {auth_token}"}
    
    # Jouw C# code checkt: string.IsNullOrWhiteSpace(req.Name) etc.
    # Als we velden leeg laten of weglaten, verwachten we een 400 Bad Request
    payload = {
        "name": "", # Leeg
        "email": "valid@email.com",
        "phoneNumber": "061234",
        "birthYear": 2000
    }
    
    r = requests.put(f"{base_url}/profile", json=payload, headers=headers, timeout=5)
    
    # C# validation logic returns BadRequest (400)
    assert r.status_code == 400, f"Expected 400 for empty name, got: {r.status_code} {r.text}"

def test_update_profile_unauthenticated(base_url):
    payload = {
        "name": "Hacker", 
        "email": "hacker@test.com", 
        "phoneNumber": "112", 
        "birthYear": 2000
    }
    r = requests.put(f"{base_url}/profile", json=payload, timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"
>>>>>>> origin/Rens-new
