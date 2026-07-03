import fs from 'fs';
import { randomUUID } from 'crypto';

const API_URL = 'http://localhost:5015/api';
const BRANCH_ID = '11111111-1111-1111-1111-111111111111';

async function run() {
  const adminLoginRes = await fetch(`${API_URL}/auth/admin/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: "admin@appleesports.com", password: "12345" })
  });
  const adminData = await adminLoginRes.json();
  if (!adminData.success) {
    console.error("Login failed:", adminData);
    return;
  }
  const token = adminData.data.accessToken;

  const membersRes = await fetch(`${API_URL}/members?branchId=${BRANCH_ID}`, {
    method: 'GET',
    headers: { 'Authorization': `Bearer ${token}` }
  });
  const members = await membersRes.json();
  console.log("Members: ", JSON.stringify(members.data.slice(0, 3), null, 2));
}

run();
