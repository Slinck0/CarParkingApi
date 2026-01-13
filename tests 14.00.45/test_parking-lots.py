import pytest
import requests
import uuid
import time
import random
import os

BASE_URL = os.getenv("BASE_URL", "http://localhost:5000")

@pytest.fixture
def base_url():
    return BASE_URL

@pytest.fixture
def make_user(base_url):
    """Hulpfunctie om een NIEUWE (niet-admin) user aan te maken voor tests"""
    def _make_user(username_prefix, role="User"):
        unique = f"{int(time.time())}_{uuid.uuid4().hex[:6]}"
        phone = f"06{random.randint(10000000, 99999999)}"
        
        reg_payload = {
            "Username": f"{username_prefix}_{unique}",
            "Password": "Password123!",
            "Name": f"Test {username_prefix}",
            "Email": f"{username_prefix}_{unique}@test.com",
            "PhoneNumber": phone,
            "BirthYear": 1990,
            "Role": "ADMIN"
        }

        # Registreer de user
        requests.post(f"{base_url}/register", json=reg_payload, timeout=5)
        
        # Login
        login_creds = {
            "Username": reg_payload["Username"],
            "Password": reg_payload["Password"]
        }
        
        r = requests.post(f"{base_url}/login", json=login_creds, timeout=5)
        assert r.status_code == 200, f"Login mislukt voor nieuwe user: {r.status_code} {r.text}"

        token = r.json().get("token")
        assert token, f"Geen token ontvangen: {r.text}"
        return token
    return _make_user

@pytest.fixture
def admin_token(base_url):
    """Logt in met de BESTAANDE admin account (Rens/Rens)"""
    login_creds = {
        "Username": "Rens",
        "Password": "Rens"
    }
    
    r = requests.post(f"{base_url}/login", json=login_creds, timeout=5)
    assert r.status_code == 200, f"Admin login (Rens) mislukt: {r.status_code} {r.text}"
    
    token = r.json().get("token")
    assert token, f"Geen token ontvangen voor admin: {r.text}"
    return token

@pytest.fixture
def user_token(make_user):
    """Maakt een standaard user aan voor de Forbidden test"""
    return make_user("user", role="User")

def post_parking_lot(base_url, token=None, payload=None):
    headers = {"Authorization": f"Bearer {token}"} if token else {}
    
    default_payload = {
        "Name": f"Garage {uuid.uuid4().hex[:4]}",
        "Location": "Rotterdam",
        "Address": "Coolsingel 1",
        "Capacity": 100,
        "Reserved": 0,
        "Tariff": 5.00,
        "DayTariff": 25.00,
        "Lat": 51.9225,
        "Lng": 4.4791,
        "Status": "Open"
    }
    
    data = payload if payload else default_payload
    return requests.post(f"{base_url}/parking-lots", json=data, headers=headers, timeout=5)

def test_create_parking_lot_success(base_url, admin_token):
    # Gebruikt nu de token van 'Rens'
    r = post_parking_lot(base_url, token=admin_token)
    assert r.status_code in (200, 201), f"Verwacht 201, kreeg: {r.status_code} {r.text}"
    
def test_create_parking_lot_unauthorized(base_url):
    r = post_parking_lot(base_url)
    assert r.status_code == 401, f"Verwacht 401, kreeg: {r.status_code} {r.text}"

def test_create_parking_lot_forbidden_for_user(base_url, user_token):
    # Gebruikt een gewone user, zou forbidden moeten zijn
    r = post_parking_lot(base_url, token=user_token)
    assert r.status_code == 403, f"Verwacht 403, kreeg: {r.status_code} {r.text}"