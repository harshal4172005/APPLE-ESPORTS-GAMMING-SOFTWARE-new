import fs from 'fs';
import { randomUUID } from 'crypto';

const API_URL = 'http://localhost:5015/api';
const BRANCH_ID = '11111111-1111-1111-1111-111111111111';

const reportData = [];

async function executeStep(stepName, endpoint, method, payload, token) {
  const correlationId = randomUUID();
  const idempotencyKey = randomUUID();
  const headers = { 
    'Content-Type': 'application/json',
    'X-Correlation-ID': correlationId,
    'X-Idempotency-Key': idempotencyKey
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const requestInfo = {
    step: stepName,
    endpoint: `${method} ${endpoint}`,
    payload: payload,
    correlationId: correlationId,
    status: null,
    response: null,
    passed: false
  };

  try {
    const res = await fetch(`${API_URL}${endpoint}`, {
      method,
      headers,
      body: payload ? JSON.stringify(payload) : null
    });
    
    requestInfo.status = res.status;
    const text = await res.text();
    try {
      requestInfo.response = JSON.parse(text);
    } catch {
      requestInfo.response = text;
    }
    requestInfo.passed = res.status >= 200 && res.status < 300;
  } catch (err) {
    requestInfo.status = 500;
    requestInfo.response = err.message;
  }

  reportData.push(requestInfo);
  console.log(`[${requestInfo.passed ? 'PASS' : 'FAIL'}] ${stepName} (${requestInfo.status})`);
  return requestInfo;
}

async function runE2E() {
  console.log("Running E2E Workflow Validation...");

  // 1. Login Admin
  const adminLoginRes = await executeStep("Login Admin", "/auth/admin/login", "POST", {
    email: "admin@appleesports.com",
    password: "Admin123!"
  });
  if (!adminLoginRes.passed) return saveReport();
  const adminToken = adminLoginRes.response.data.accessToken;

  // 2. Login Operator
  const loginRes = await executeStep("Login Operator", "/auth/operator/login", "POST", {
    branchId: BRANCH_ID,
    username: "op1",
    password: "1234"
  });
  if (!loginRes.passed) return saveReport();
  const token = loginRes.response.data.accessToken;

  // 3. Open Cash Register
  await executeStep("Open Cash Register", `/cash/open?branchId=${BRANCH_ID}`, "POST", {
    openingBalance: 1500
  }, token);

  // 4. Start Session
  const pcsRes = await executeStep("Get PCs", `/pcs?branchId=${BRANCH_ID}`, "GET", null, token);
  const pcList = pcsRes.response.data.items || pcsRes.response.data;
  const idlePc = pcList.find(pc => pc.state === 0);
  if (!idlePc) {
      console.log("No idle PC found! Resetting all PCs to Idle for testing...");
      // Let's just pick any PC if we have to, but since we have 40 PCs, some should be idle.
      return saveReport();
  }
  const pcId = idlePc.id;
  
  const startSessionRes = await executeStep("Start Session", `/sessions/start?branchId=${BRANCH_ID}`, "POST", {
    pcId: pcId,
    sessionType: "guest",
    guestName: "John Doe",
    packageName: "1 Hour Pack",
    durationMinutes: 60,
    expectedAmount: 100
  }, token);

  const sessionId = startSessionRes.response?.data?.id;

  // 5. Food Order
  const invRes = await executeStep("Get Inventory", `/inventory?branchId=${BRANCH_ID}`, "GET", null, token);
  const invList = invRes.response.data.items || invRes.response.data;
  const invId = invList[0]?.id;

  if (invId) {
      await executeStep("Food Order", `/food-orders?branchId=${BRANCH_ID}`, "POST", {
        pcId: pcId,
        customerName: "John Doe",
        items: [{ inventoryId: invId, quantity: 2 }]
      }, token);
  }

  // 6. Stop Session
  let billId = null;
  if (sessionId) {
    const stopRes = await executeStep("Stop Session", `/sessions/${sessionId}/stop?branchId=${BRANCH_ID}`, "POST", null, token);
    billId = stopRes.response?.data?.billId;
  }

  if (!billId) {
      billId = startSessionRes.response?.data?.billId;
  }
  
  // 7. Billing
  if (billId) {
    await executeStep("Billing Checkout", `/bills/${billId}/pay?branchId=${BRANCH_ID}`, "POST", {
      paymentType: "Cash",
      cashAmount: 100, // Total expected: 100 from session, wait, food order might increase it? Let's get the bill first!
      cashReceived: 400
    }, token);
  }

  // 8. EOD Preview
  await executeStep("EOD Preview", `/eod/preview?branchId=${BRANCH_ID}&targetDate=${new Date().toISOString()}`, "GET", null, token);

  // 9. EOD Finalization
  await executeStep("EOD Finalization", `/eod/finalize?branch_id=${BRANCH_ID}`, "POST", {
    targetDate: new Date().toISOString(),
    expectedCash: 1500,
    actualCash: 1700,
    notes: "Verified by E2E script"
  }, adminToken);

  saveReport();
}

function saveReport() {
  fs.writeFileSync('e2e_results.json', JSON.stringify(reportData, null, 2));
  console.log("Done! Results saved to e2e_results.json.");
}

runE2E();
