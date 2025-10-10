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
    requests.post(f"{base_url}/register", json={"name": "Test User", **creds}, timeout=5)

    r = requests.post(f"{base_url}/login", json=creds, timeout=5)
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

    
   
    

    
    


   