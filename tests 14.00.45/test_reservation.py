import pytest
import requests

API_VERSION = "v2"  
BASE_URL = "http://localhost:8000" if API_VERSION == "v1" else "http://localhost:5000"

def auth_headers(token: str) -> dict:
    if API_VERSION == "v2":
        return {"Authorization": f"Bearer {token}"}
    return {"Authorization": token}

def build_register_payload(creds):
    if API_VERSION == "v1":
        return {
            "username": creds["username"],
            "password": creds["password"],
            "name": "Test User"
        }
    else:  # v2
        return {
            "Username": creds["username"],
            "Password": creds["password"],
            "Name": "Test User",
            "Email": "test@example.com",
            "PhoneNumber": "0612345678",
            "BirthYear": 2000
        }

def build_login_payload(creds):
    if API_VERSION == "v1":
        return {
            "username": creds["username"],
            "password": creds["password"]
        }
    else:
        return {
            "Username": creds["username"],
            "Password": creds["password"]
        }

def json_or_text(resp: requests.Response):
    try:
        return resp.json()
    except ValueError:
        return {"_raw": resp.text}

@pytest.fixture
def base_url():
    return BASE_URL

@pytest.fixture
def creds():
    return {"username": "tesuer", "password": "testuserpass"}

@pytest.fixture
def auth_token(base_url, creds):

    try:
        requests.post(f"{base_url}/register", json=build_register_payload(creds), timeout=5)
    except requests.RequestException:
        pass

    # Login
    r = requests.post(f"{base_url}/login", json=build_login_payload(creds), timeout=5)
    assert r.status_code in (200, 201), f"Login faalt: {r.status_code} {r.text}"

    data = json_or_text(r)
    token = data.get("token") or data.get("session_token")
    assert token, f"Geen token in response: {data}"
    return token

@pytest.fixture
def reservation_payload():
    if API_VERSION == "v1":
        return {
            "licenseplate": "75-KQQ-7",
            "startdate": "2025-12-03 10:00:00",
            "enddate": "2025-12-05 10:00:00",
            "parkinglot": 1,
        }
    else:
        return {
            "LicensePlate": "75-KQQ-7",
            "StartDate": "2025-12-03",
            "EndDate": "2025-12-05",
            "ParkingLot": 1,
            "VehicleId": 123    
        }

@pytest.fixture
def reservation_payload_missing():
        return {
            "LicensePlate": "75-KQQ-7",
            "StartDate": "2025-12-03",
            "EndDate": "2025-12-05",
            "ParkingLot": 1,
            
        }

def test_create_reservation(base_url, auth_token, reservation_payload):
    r = requests.post(
        f"{base_url}/reservations",
        json=reservation_payload,
        headers=auth_headers(auth_token),
        timeout=5,
    )

    assert r.status_code in (200, 201), f"{r.status_code} {r.text}"
    body = json_or_text(r)

    if API_VERSION == "v2":
        assert isinstance(body, dict), f"v2: unexpected body type: {body}"
        assert "reservation" in body or "status" in body, f"v2: missing fields {body}"
    else:
        assert "Success" in str(body) or "reservation" in str(body), f"v1: unexpected {body}"

def test_create_reservation_bad_missing_field(base_url, auth_token, reservation_payload_missing):
    r = requests.post(
        f"{base_url}/reservations",
        json=reservation_payload_missing,
        headers=auth_headers(auth_token),
        timeout=5,
    )
    assert 400 <= r.status_code < 500, f"Verwacht 4xx, kreeg: {r.status_code} {r.text}"
    body = json_or_text(r)
    assert any(k in body for k in ("error", "message", "detail", "errors", "_raw")), \
        f"Geen foutinformatie in body: {body}"

def test_create_reservation_unauthenticated(base_url, reservation_payload):
    r = requests.post(
        f"{base_url}/reservations",
        json=reservation_payload,
        timeout=5,
    )
    assert r.status_code == 401, f"Verwacht 401, kreeg: {r.status_code} {r.text}"

def test_edit_reservation(base_url, reservation_payload, auth_token):
    r = requests.put(
        f"{base_url}/reservations/2010",
        json=reservation_payload,
        headers=auth_headers(auth_token),
        timeout=5,
    )
    assert r.status_code in (404, 403), f"Verwacht 404/403, kreeg: {r.status_code} {r.text}"

def test_remove_reservation(base_url, reservation_payload, auth_token):
    r = requests.delete(
        f"{base_url}/reservations/2010",
        json=reservation_payload,
        headers=auth_headers(auth_token),
        timeout=5,
    )
    assert r.status_code in (404, 403), f"Verwacht 404/403, kreeg: {r.status_code} {r.text}"

def test_get_reservations(base_url, auth_token):
    if API_VERSION == "v1":
        r = requests.get(
            f"{base_url}/reservations/1",
            headers=auth_headers(auth_token),
            timeout=5,
        )
        assert r.status_code in (200,), f"Verwacht 200 of 404, kreeg: {r.status_code} {r.text}"
    else:
        r = requests.get(
            f"{base_url}/reservations/me",
            headers=auth_headers(auth_token),
            timeout=5,
        )
        assert r.status_code in (200,), f"Verwacht 200 of 404, kreeg: {r.status_code} {r.text}"


