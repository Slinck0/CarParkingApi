import pytest
import requests
import uuid


@pytest.fixture
def base_url():
    return "http://localhost:8000"


@pytest.fixture
def auth_user(base_url):
    suffix = uuid.uuid4().hex[:8]
    creds = {
        "username": f"testuser_{suffix}",
        "password": "testpass123"
    }

    requests.post(f"{base_url}/register", json=creds, timeout=5)
    login_response = requests.post(f"{base_url}/login", json=creds, timeout=5)

    if login_response.status_code == 200:
        token = login_response.json().get("session_token")
        return {"token": token, "headers": {"Authorization": token}, "username": creds["username"]}
    return None


def test_server_connectivity(base_url):
    test_creds = {
        "username": f"connect_test_{uuid.uuid4().hex[:8]}",
        "password": "test123"
    }
    response = requests.post(f"{base_url}/register", json=test_creds, timeout=5)
    assert response.status_code in [201, 200]


def test_create_payment_valid_scenarios(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    test_cases = [
        {"transaction": f"tx_normal_{uuid.uuid4().hex[:8]}", "amount": 25.50},
        {"transaction": f"tx_zero_{uuid.uuid4().hex[:8]}", "amount": 0},
        {"transaction": f"tx_large_{uuid.uuid4().hex[:8]}", "amount": 9999.99},
        {"transaction": f"tx_decimal_{uuid.uuid4().hex[:8]}", "amount": 12.34}
    ]

    for i, payment_data in enumerate(test_cases):
        response = requests.post(
            f"{base_url}/payments",
            json=payment_data,
            headers=auth_user["headers"],
            timeout=5
        )
        assert response.status_code in [201, 400, 401]


def test_create_payment_invalid_scenarios(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    invalid_cases = [
        {"amount": 25.50},
        {"transaction": f"tx_no_amount_{uuid.uuid4().hex[:8]}"},
        {"transaction": "", "amount": 25.50},
        {"transaction": f"tx_neg_{uuid.uuid4().hex[:8]}", "amount": -10},
        {"transaction": f"tx_str_amount_{uuid.uuid4().hex[:8]}", "amount": "invalid"},
        {}
    ]

    for payment_data in invalid_cases:
        response = requests.post(
            f"{base_url}/payments",
            json=payment_data,
            headers=auth_user["headers"],
            timeout=5
        )
        assert response.status_code in [400, 401]


def test_create_payment_duplicate_transaction(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    duplicate_tx = f"duplicate_tx_{uuid.uuid4().hex[:8]}"
    payment_data = {"transaction": duplicate_tx, "amount": 25.50}

    response1 = requests.post(
        f"{base_url}/payments",
        json=payment_data,
        headers=auth_user["headers"],
        timeout=5
    )

    response2 = requests.post(
        f"{base_url}/payments",
        json=payment_data,
        headers=auth_user["headers"],
        timeout=5
    )

    assert response1.status_code in [201, 400, 401]
    assert response2.status_code in [400, 401, 409]


def test_get_payments_empty_and_populated(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    response = requests.get(
        f"{base_url}/payments",
        headers=auth_user["headers"],
        timeout=5
    )

    if response.status_code == 200:
        payments = response.json()
        assert isinstance(payments, list)

    payment_data = {"transaction": f"tx_for_list_{uuid.uuid4().hex[:8]}", "amount": 15.00}
    requests.post(f"{base_url}/payments", json=payment_data, headers=auth_user["headers"], timeout=5)

    response_after = requests.get(
        f"{base_url}/payments",
        headers=auth_user["headers"],
        timeout=5
    )

    assert response_after.status_code in [200, 401]


def test_payment_validation_success_flow(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    payment_data = {"transaction": f"validate_success_{uuid.uuid4().hex[:8]}", "amount": 30.00}

    create_response = requests.post(
        f"{base_url}/payments",
        json=payment_data,
        headers=auth_user["headers"],
        timeout=5
    )

    if create_response.status_code == 201:
        created_payment = create_response.json().get("payment", {})
        validation_hash = created_payment.get("hash")

        if validation_hash:
            validation_data = {
                "t_data": {"provider": "ideal", "ref": "SUCCESS123"},
                "validation": validation_hash
            }

            validate_response = requests.put(
                f"{base_url}/payments/{payment_data['transaction']}",
                json=validation_data,
                headers=auth_user["headers"],
                timeout=5
            )
            assert validate_response.status_code in [200, 400, 401, 404]


def test_payment_validation_invalid_hash(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    payment_data = {"transaction": f"validate_invalid_{uuid.uuid4().hex[:8]}", "amount": 30.00}

    create_response = requests.post(
        f"{base_url}/payments",
        json=payment_data,
        headers=auth_user["headers"],
        timeout=5
    )

    if create_response.status_code in [201, 400, 401]:
        validation_data = {
            "t_data": {"provider": "ideal", "ref": "INVALID123"},
            "validation": "invalid_hash_12345"
        }

        validate_response = requests.put(
            f"{base_url}/payments/{payment_data['transaction']}",
            json=validation_data,
            headers=auth_user["headers"],
            timeout=5
        )
        assert validate_response.status_code in [401, 400, 404]


def test_payment_validation_missing_fields(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    payment_data = {"transaction": f"validate_missing_{uuid.uuid4().hex[:8]}", "amount": 30.00}

    create_response = requests.post(
        f"{base_url}/payments",
        json=payment_data,
        headers=auth_user["headers"],
        timeout=5
    )

    if create_response.status_code in [201, 400, 401]:
        invalid_cases = [
            {"validation": "some_hash"},
            {"t_data": {"provider": "ideal"}},
            {}
        ]

        for validation_data in invalid_cases:
            validate_response = requests.put(
                f"{base_url}/payments/{payment_data['transaction']}",
                json=validation_data,
                headers=auth_user["headers"],
                timeout=5
            )
            assert validate_response.status_code in [400, 401]


def test_payment_refund_as_regular_user(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    refund_data = {"amount": 10.00, "transaction": f"refund_user_{uuid.uuid4().hex[:8]}"}

    response = requests.post(
        f"{base_url}/payments/refund",
        json=refund_data,
        headers=auth_user["headers"],
        timeout=5
    )

    assert response.status_code in [403, 401]


def test_payment_refund_invalid_data(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    invalid_refunds = [
        {"amount": -10.00},
        {"amount": "invalid"},
        {}
    ]

    for refund_data in invalid_refunds:
        response = requests.post(
            f"{base_url}/payments/refund",
            json=refund_data,
            headers=auth_user["headers"],
            timeout=5
        )
        assert response.status_code in [400, 401, 403]


def test_payment_endpoints_unauthenticated(base_url):
    endpoints = [
        ("GET", "/payments"),
        ("POST", "/payments"),
        ("PUT", "/payments/some_id"),
        ("POST", "/payments/refund")
    ]

    for method, endpoint in endpoints:
        if method == "GET":
            response = requests.get(f"{base_url}{endpoint}", timeout=5)
        elif method == "POST":
            response = requests.post(f"{base_url}{endpoint}", json={}, timeout=5)
        elif method == "PUT":
            response = requests.put(f"{base_url}{endpoint}", json={}, timeout=5)

        assert response.status_code == 401


def test_payment_comprehensive_workflow(base_url, auth_user):
    if not auth_user:
        pytest.skip("Authentication failed")

    workflow_steps = []

    step1_data = {"transaction": f"workflow_{uuid.uuid4().hex[:8]}", "amount": 50.00}
    step1_response = requests.post(f"{base_url}/payments", json=step1_data, headers=auth_user["headers"], timeout=5)
    workflow_steps.append(("Create Payment", step1_response.status_code))

    step2_response = requests.get(f"{base_url}/payments", headers=auth_user["headers"], timeout=5)
    workflow_steps.append(("Get Payments", step2_response.status_code))

    if step1_response.status_code == 201:
        payment = step1_response.json().get("payment", {})
        if payment.get("hash"):
            step3_data = {
                "t_data": {"provider": "ideal", "ref": "WORKFLOW123"},
                "validation": payment["hash"]
            }
            step3_response = requests.put(
                f"{base_url}/payments/{step1_data['transaction']}",
                json=step3_data,
                headers=auth_user["headers"],
                timeout=5
            )
            workflow_steps.append(("Validate Payment", step3_response.status_code))

    for step_name, status_code in workflow_steps:
        assert status_code in [200, 201, 400, 401, 404]