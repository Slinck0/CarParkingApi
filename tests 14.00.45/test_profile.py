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

def test_get_profile_unauthenticated(base_url):
    r = requests.get(f"{base_url}/profile", timeout=5)
    assert r.status_code == 401, f"Expected 401, got: {r.status_code} {r.text}"

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