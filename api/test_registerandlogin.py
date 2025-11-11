import pytest
import requests
import json

@pytest.fixture
def base_url():
    return 'http://localhost:8000'

@pytest.fixture
def test_user():
    return {
        'username': 'testuser',
        'password': 'testpass123',
        'name': 'Test User'
    }

def test_register(base_url, test_user):
    """Test registering a new user"""
    url = f"{base_url}/register"
    response = requests.post(url, json=test_user)
    
    assert response.status_code == 201
    assert response.content == b"User created"

def test_register_duplicate(base_url, test_user):
    """Test registering a duplicate user"""
    url = f"{base_url}/register"
    response = requests.post(url, json=test_user)
    
    assert response.status_code == 200
    assert response.content == b"Username already taken"

def test_login_success(base_url, test_user):
    """Test successful login"""
    url = f"{base_url}/login"
    
    login_data = {
        'username': test_user['username'],
        'password': test_user['password']
    }
    response = requests.post(url, json=login_data)
    assert response.status_code == 200

def test_login_wrong_password(base_url, test_user):
    """Test login with wrong password"""
    url = f"{base_url}/login"
    
    login_data = {
        'username': test_user['username'],
        'password': 'wrongpassword'
    }
    response = requests.post(url, json=login_data)
    assert response.status_code == 401