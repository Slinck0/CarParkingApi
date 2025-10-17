import pytest
import requests

@pytest.fixture
def base_url():
    return "http://localhost:8000"

@pytest.fixture
def creds():
    return {"username": "testuser", "password": "testuserpass"}

@pytest.fixture
def auth_token(base_url, creds):
    requests.post(f"{base_url}/register", json={"name": "Test User", **creds}, timeout=5)

    r = requests.post(f"{base_url}/login", json=creds, timeout=5)
    assert r.status_code in (200, 201), f"Login faalt: {r.status_code} {r.text}"
    data = r.json()
    token = data.get("session_token")
    assert token, f"Geen session_token in response: {data}"
    return token  
@pytest.fixture
def auth_token_admin(base_url):
    creds_admin = {"username": "Rens1", "password": "Rens"}
    r = requests.post(f"{base_url}/login", json=creds_admin, timeout=5)
    assert r.status_code in (200, 201), f"Login faalt: {r.status_code} {r.text}"
    data = r.json()
    token = data.get("session_token")
    assert token, f"Geen session_token in response: {data}"
    return token

@pytest.fixture
def reservation_payload():
    return {
        "licenseplate": "75-KQQ-7",
        "startdate": "2025-12-03",
        "enddate": "2025-12-05",
        "parkinglot": "1"
        
    }
@pytest.fixture
def reservation_payload_missing():
    return {
        "licenseplate": "75-KQQ-7",
        "startdate": "2025-12-03",
        "enddate": "2025-12-05"
        
        
    }
def json_or_text(resp: requests.Response):
    try:
        return resp.json()
    except ValueError:
        return {"_raw": resp.text}

def test_create_reservation(base_url, auth_token, reservation_payload):
    headers = {"Authorization": auth_token}  
    r = requests.post(f"{base_url}/reservations", json=reservation_payload, headers=headers, timeout=5)

    assert r.status_code in (200, 201), f"{r.status_code} {r.text}"
    body = r.json()

    assert body.get("status") == "Success"
    assert isinstance(body.get("reservation"), dict)
    res = body["reservation"]

    
    assert res["licenseplate"] == reservation_payload["licenseplate"]
    assert res["startdate"]    == reservation_payload["startdate"]
    assert res["enddate"]      == reservation_payload["enddate"]
    assert str(res["parkinglot"]) == str(reservation_payload["parkinglot"])

def test_create_reservation_bad_missing_field(base_url, auth_token, reservation_payload_missing):
    r = requests.post(
        f"{base_url}/reservations",
        json=reservation_payload_missing,
        headers= {"Authorization": auth_token},
        timeout=5,
    )

    assert 400 <= r.status_code < 500, f"Verwacht 4xx, kreeg: {r.status_code} {r.text}"

    body = json_or_text(r)
    assert any(k in body for k in ("error", "message", "detail", "errors")), \
        f"Geen foutinformatie in body: {body}"

def test_create_reservation_unauthenticated(base_url, reservation_payload):
    r = requests.post(
        f"{base_url}/reservations",
        json=reservation_payload,
        timeout=5,
    )

    assert r.status_code == 401, f"Verwacht 401, kreeg: {r.status_code} {r.text}"

def test_edit_reseravation(base_url, reservation_payload,auth_token):

    r = requests.put(
        f"{base_url}/reservations/2010",
        json=reservation_payload,
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code in (200, 204), f"Verwacht 200 of 204, kreeg: {r.status_code} {r.text}"

def test_remove_reservation(base_url, reservation_payload,auth_token):

    r = requests.delete(
        f"{base_url}/reservations/2010",
        json=reservation_payload,
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code in (200, 204), f"Verwacht 200 of 204, kreeg: {r.status_code} {r.text}"

def test_get_reservations(base_url, auth_token):

    r = requests.get(
        f"{base_url}/reservations",
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code in (200, 204), f"Verwacht 200 of 204, kreeg: {r.status_code} {r.text}"

   
    

    
    


   