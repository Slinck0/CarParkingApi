import uuid
import requests
import pytest

from test_profile import base_url, creds, auth_token, json_or_text

@pytest.fixture
def plate():
    raw = str(uuid.uuid4())[:7].upper()
    return f"{raw[:2]}-{raw[2:5]}-{raw[5:]}"  # e.g., AB-123-CD

def lid_from_plate(plate: str) -> str:
    return plate.replace("-", "")

def test_create_vehicle_ok(base_url, auth_token, plate):
    r = requests.post(
        f"{base_url}/vehicles",
        json={"name": "Daily Driver", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 201, f"{r.status_code} {r.text}"
    body = r.json()
    assert body.get("status") == "Success"
    assert body.get("vehicle", {}).get("license_plate") == plate

def test_create_vehicle_missing_fields(base_url, auth_token, plate):
    # Missing name
    r1 = requests.post(
        f"{base_url}/vehicles",
        json={"license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r1.status_code == 401, f"{r1.status_code} {r1.text}"
    # Missing license_plate
    r2 = requests.post(
        f"{base_url}/vehicles",
        json={"name": "No Plate"},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r2.status_code == 401, f"{r2.status_code} {r2.text}"

def test_list_vehicles_contains_created_vehicle(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "Touring", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    r = requests.get(
        f"{base_url}/vehicles",
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    body = r.json()
    assert lid_from_plate(plate) in body, f"Vehicle {plate} not found in list"

def test_vehicle_entry_ok(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "Commute", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)
    r = requests.post(
        f"{base_url}/vehicles/{lid}/entry",
        json={"parkinglot": "1"},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    assert r.json().get("status") == "Accepted"

def test_vehicle_entry_missing_parkinglot(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "Errand", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)
    r = requests.post(
        f"{base_url}/vehicles/{lid}/entry",
        json={},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 401, f"{r.status_code} {r.text}"

def test_get_vehicle_reservations_and_history(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "TripCar", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)

    r1 = requests.get(
        f"{base_url}/vehicles/{lid}/reservations",
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r1.status_code == 200, f"{r1.status_code} {r1.text}"
    assert r1.json() == []

    r2 = requests.get(
        f"{base_url}/vehicles/{lid}/history",
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r2.status_code == 200, f"{r2.status_code} {r2.text}"
    assert r2.json() == []

def test_update_vehicle_name_ok(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "OldName", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)
    r = requests.put(
        f"{base_url}/vehicles/{lid}",
        json={"name": "NewName"},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    body = r.json()
    assert body.get("status") == "Success"
    assert body.get("vehicle", {}).get("name") == "NewName"

def test_update_vehicle_missing_name(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "Keep", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)
    r = requests.put(
        f"{base_url}/vehicles/{lid}",
        json={},  # missing 'name'
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 401, f"{r.status_code} {r.text}"

def test_delete_vehicle_ok(base_url, auth_token, plate):
    requests.post(
        f"{base_url}/vehicles",
        json={"name": "ToDelete", "license_plate": plate},
        headers={"Authorization": auth_token},
        timeout=5,
    )
    lid = lid_from_plate(plate)
    r = requests.delete(
        f"{base_url}/vehicles/{lid}",
        headers={"Authorization": auth_token},
        timeout=5,
    )
    assert r.status_code == 200, f"{r.status_code} {r.text}"
    assert r.json().get("status") == "Deleted"

def test_vehicle_unauthenticated_requests(base_url, plate):
    r1 = requests.get(f"{base_url}/vehicles", timeout=5)
    assert r1.status_code == 401, f"{r1.status_code} {r1.text}"

    r2 = requests.post(
        f"{base_url}/vehicles",
        json={"name": "NoAuth", "license_plate": plate},
        timeout=5,
    )
    assert r2.status_code == 401, f"{r2.status_code} {r2.text}"
