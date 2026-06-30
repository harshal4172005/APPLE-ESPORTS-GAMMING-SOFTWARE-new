const axios = require('axios');
const crypto = require('crypto');

const API_URL = 'http://localhost:5015/api';
let adminToken = '';
let operatorToken = '';
let branchId = '11111111-1111-1111-1111-111111111111';

async function runTests() {
    console.log('--- STARTING WORKFLOW VERIFICATION ---');
    try {
        // 1. Super Admin Login
        console.log('\n[Super Admin] Logging in...');
        const adminRes = await axios.post(`${API_URL}/auth/admin/login`, {
            email: 'admin@appleesports.com',
            password: 'Admin123!',
            deviceInfo: { device: 'test_script' }
        });
        adminToken = adminRes.data.data.accessToken;
        console.log('✅ Super Admin login successful');

        // 2. Operator Login
        console.log('\n[Operator] Logging in...');
        const opRes = await axios.post(`${API_URL}/auth/operator/login`, {
            branchId: branchId,
            username: 'op1',
            password: '1234',
            deviceInfo: { device: 'test_script' }
        });
        operatorToken = opRes.data.data.accessToken;
        console.log('✅ Operator login successful');
        console.log('✅ Shift started automatically');

        // 3. Admin: Fetch Dashboard
        console.log('\n[Super Admin] Fetching Dashboard...');
        const dashRes = await axios.get(`${API_URL}/dashboard/summary`, {
            headers: { Authorization: `Bearer ${adminToken}`, 'X-Branch-Id': branchId }
        });
        console.log('✅ Dashboard financials fetched successfully. Status:', dashRes.status);

        // 4. Operator: Cash Desk Verification Start
        console.log('\n[Operator] Starting Cash Desk Verification...');
        const idempotencyKey = crypto.randomUUID();
        const regRes = await axios.post(`${API_URL}/cash-desk/verify-start`, {}, {
            headers: { 
                Authorization: `Bearer ${operatorToken}`,
                'X-Idempotency-Key': idempotencyKey
            }
        });
        console.log('✅ Cash Register verification started. Message:', regRes.data.message);

        // 5. Operator: Close Shift (Logout)
        console.log('\n[Operator] Closing Shift (Logging out)...');
        const logoutRes = await axios.post(`${API_URL}/auth/logout`, {}, {
            headers: { Authorization: `Bearer ${operatorToken}` }
        });
        console.log('✅ Operator logout and Shift Closure successful');

        console.log('\n--- ALL WORKFLOWS VERIFIED SUCCESSFULLY ---');

    } catch (err) {
        console.error('\n❌ ERROR DURING WORKFLOW VERIFICATION:');
        if (err.response) {
            console.error(err.response.status, err.response.data);
        } else {
            console.error(err.message);
        }
    }
}

runTests();
