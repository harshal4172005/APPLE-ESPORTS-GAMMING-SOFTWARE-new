import axios from 'axios';

const API = 'http://localhost:5015/api';

async function probe() {
  console.log('═══════════════════════════════════════');
  console.log('P0 ROOT CAUSE ANALYSIS — Live Probes');
  console.log('═══════════════════════════════════════\n');

  // ─── STEP 1: Login as Super Admin ───
  console.log('▶ STEP 1: Super Admin Login');
  let adminToken;
  try {
    const loginRes = await axios.post(`${API}/auth/admin/login`, {
      email: 'admin@appleesports.com',
      password: 'Admin123!'
    });
    adminToken = loginRes.data.data.accessToken;
    const user = loginRes.data.data.user;
    console.log(`  ✓ Login OK | Token length: ${adminToken.length}`);
    console.log(`  User: ${JSON.stringify(user, null, 2)}\n`);
  } catch (e) {
    console.log(`  ✗ Login FAILED: ${e.response?.status} ${JSON.stringify(e.response?.data)}\n`);
    return;
  }

  const headers = { Authorization: `Bearer ${adminToken}` };
  const branchId = '11111111-1111-1111-1111-111111111111';

  // ─── STEP 2: Login as Operator ───
  console.log('▶ STEP 2: Operator Login');
  let opToken, opUser;
  try {
    const opRes = await axios.post(`${API}/auth/operator/login`, {
      branchId,
      username: 'op1',
      password: '1234'
    });
    opToken = opRes.data.data.accessToken;
    opUser = opRes.data.data.user;
    console.log(`  ✓ Operator Login OK | Token length: ${opToken.length}`);
    console.log(`  User: ${JSON.stringify(opUser, null, 2)}\n`);
  } catch (e) {
    console.log(`  ✗ Operator Login FAILED: ${e.response?.status} ${JSON.stringify(e.response?.data)}\n`);
  }

  const opHeaders = opToken ? { Authorization: `Bearer ${opToken}` } : headers;

  // ─── PROBE A: Cash Register — GET active (as operator) ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE A: Cash Register GET /cash/active');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/cash/active`, { headers: opHeaders, params: { branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE B: Cash Register — POST open (as operator, valid balance) ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE B: Cash Register POST /cash/open (balance=500)');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.post(`${API}/cash/open`, { openingBalance: 500 }, { headers: opHeaders });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE C: Cash Register — POST open (as admin, NO shift) ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE C: Cash Register POST /cash/open (as ADMIN, balance=500)');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.post(`${API}/cash/open`, { openingBalance: 500 }, { headers });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE D: Cash Register — POST open (negative balance) ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE D: Cash Register POST /cash/open (balance=-100)');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.post(`${API}/cash/open`, { openingBalance: -100 }, { headers: opHeaders });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE E: EOD — GET /eod/preview ───
  const today = new Date().toISOString().split('T')[0];
  console.log('═══════════════════════════════════════');
  console.log(`PROBE E: EOD GET /eod/history (date=${today})`);
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/eod/history`, { headers: opHeaders, params: { date: today, branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  console.log('═══════════════════════════════════════');
  console.log(`PROBE F: EOD GET /eod/preview (date=${today})`);
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/eod/preview`, { headers: opHeaders, params: { date: today, branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  console.log('═══════════════════════════════════════');
  console.log(`PROBE G: EOD GET /eod/validation (date=${today})`);
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/eod/validation`, { headers: opHeaders, params: { date: today, branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE H: Food Orders — GET /inventory ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE H: Food Orders GET /inventory');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/inventory`, { headers: opHeaders, params: { branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE I: Food Orders — POST /food-orders (empty items array) ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE I: Food Orders POST /food-orders (empty items)');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.post(`${API}/food-orders`, {
      sessionId: null,
      pcId: null,
      customerName: 'Test',
      items: []
    }, { headers: opHeaders });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE J: Sessions — GET /pcs ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE J: Sessions GET /pcs');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/pcs`, { headers: opHeaders, params: { branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE K: Dashboard — GET /dashboard/summary ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE K: Dashboard GET /dashboard/summary');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/dashboard/summary`, { headers, params: { branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE L: Dashboard — GET /dashboard/transactions ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE L: Dashboard GET /dashboard/transactions');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/dashboard/transactions`, { headers, params: { branchId, limit: 5 } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  // ─── PROBE M: Food Orders — GET /food-orders ───
  console.log('═══════════════════════════════════════');
  console.log('PROBE M: Food Orders GET /food-orders');
  console.log('═══════════════════════════════════════');
  try {
    const res = await axios.get(`${API}/food-orders`, { headers: opHeaders, params: { page: 1, pageSize: 5, branchId } });
    console.log(`  Status: ${res.status}`);
    console.log(`  Body: ${JSON.stringify(res.data, null, 2)}\n`);
  } catch (e) {
    console.log(`  Status: ${e.response?.status}`);
    console.log(`  Body: ${JSON.stringify(e.response?.data, null, 2)}\n`);
  }

  console.log('═══════════════════════════════════════');
  console.log('ALL PROBES COMPLETE');
  console.log('═══════════════════════════════════════');
}

probe().catch(e => console.error('FATAL:', e.message));
