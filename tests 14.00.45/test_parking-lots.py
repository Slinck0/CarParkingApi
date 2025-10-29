import pytest
import requests
import uuid

@pytest.fixture
def base_url():
    return "http://localhost:8000"

@pytest.fixture
def make_user(base_url):
    # Hulp account maken voor token
    def _make_user(username_prefix, role=None):
        unique = uuid.uuid4().hex[:6]
        creds = {"username": f"{username_prefix}{unique}", "password": "testpass123"}
        if role:
            creds["role"] = role

        # Registreren
        requests.post(f"{base_url}/register", json={"name": "Test User", **creds}, timeout=5)
        # inloggen
        r = requests.post(f"{base_url}/login", json=creds, timeout=5)
        assert r.status_code in (200, 201), f"Login mislukt: {r.status_code} {r.text}"

        token = r.json().get("session_token")
        assert token, f"Geen session_token ontvangen: {r.text}"
        return token
    return _make_user

@pytest.fixture
def admin_token(make_user):
    # maakt gebruiker met admin aan
    return make_user("admin", role="admin")

@pytest.fixture
def user_token(make_user):
    # gebruiker zonder admin
    return make_user("user")

def post_parking_lot(base_url, token=None, payload=None):
    # als er een token is komt het in de header anders niet
    headers = {"Authorization": token} if token else {}
    return requests.post(f"{base_url}/parking-lots", json=payload or {}, headers=headers, timeout=5)

def test_create_parking_lot_success(base_url, admin_token):
    # admin kan goed een parking lot maken
    r = post_parking_lot(base_url, token=admin_token, payload={"Name": "P1"})
    assert r.status_code in (200, 201), f"Verwacht 200/201, kreeg: {r.status_code} {r.text}"
    assert "ID" in r.text or "saved" in r.text

def test_create_parking_lot_unauthorized(base_url):
    # geen token
    r = post_parking_lot(base_url)
    assert r.status_code == 401, f"Verwacht 401, kreeg: {r.status_code} {r.text}"
    assert "Unauthorized" in r.text

def test_create_parking_lot_forbidden_for_user(base_url, user_token):
    # niet goede rechten   
    r = post_parking_lot(base_url, token=user_token)
    assert r.status_code == 403, f"Verwacht 403, kreeg: {r.status_code} {r.text}"
    assert "Access denied" in r.text or "Forbidden" in r.text