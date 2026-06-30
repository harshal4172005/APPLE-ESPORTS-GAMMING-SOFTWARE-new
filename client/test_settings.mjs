const api = 'http://appleesports-api:8080/api';
const bid = 'b0000000-0000-0000-0000-000000000001';

async function req(path, method = 'GET', body = null, tok = null) {
  const headers = { 'Content-Type': 'application/json' };
  if (tok) headers['Authorization'] = 'Bearer ' + tok;
  const res = await fetch(api + path, {
    method,
    headers,
    body: body ? JSON.stringify(body) : null
  });
  return { status: res.status, data: await res.json() };
}

function pass(label, detail) {
  console.log('[PASS]', label, detail || '');
  return true;
}

function fail(label, data) {
  console.error('[FAIL]', label, JSON.stringify(data, null, 2));
  process.exit(1);
}

async function run() {
  console.log('\n========================================================================');
  console.log('  COMPREHENSIVE SETTINGS PAGE — END-TO-END VERIFICATION');
  console.log('========================================================================\n');

  // ═══ STEP 1: Login ═══
  const login = await req('/auth/admin/login', 'POST', { email: 'admin@appleesports.com', password: 'Admin123!' });
  if (!login.data?.data?.accessToken) fail('Admin Login', login.data);
  const tok = login.data.data.accessToken;
  pass('Admin Login', `token length: ${tok.length}`);

  // ═══ STEP 2: BRANCHES CRUD ═══
  console.log('\n--- BRANCHES ---');
  
  // 2a. GET branches
  const br = await req('/branches', 'GET', null, tok);
  if (!br.data?.data) fail('GET /branches', br.data);
  const initBranchCount = br.data.data.length;
  pass(`GET /branches`, `count: ${initBranchCount}`);

  // 2b. CREATE branch
  const cBr = await req('/branches', 'POST', { name: 'E2E Branch', address: '42 Test Ave', openingTime: '09:00', closingTime: '23:00' }, tok);
  if (!cBr.data?.data?.id) fail('POST /branches', cBr.data);
  const brId = cBr.data.data.id;
  pass('POST /branches', `id: ${brId}`);

  // 2c. Verify count incremented
  const br2 = await req('/branches', 'GET', null, tok);
  if (br2.data.data.length !== initBranchCount + 1) fail('Branch count after create', { expected: initBranchCount + 1, got: br2.data.data.length });
  pass('Branch count incremented', `${initBranchCount} → ${br2.data.data.length}`);

  // 2d. UPDATE branch
  const uBr = await req('/branches/' + brId, 'PUT', { name: 'E2E Branch UPDATED', address: '99 Changed St', openingTime: '10:00', closingTime: '22:00' }, tok);
  if (uBr.status !== 200) fail('PUT /branches', uBr.data);
  pass('PUT /branches', `name: ${uBr.data.data.name}`);

  // 2e. Verify update persisted
  const br3 = await req('/branches', 'GET', null, tok);
  const updBranch = br3.data.data.find(b => b.id === brId);
  if (!updBranch || updBranch.name !== 'E2E Branch UPDATED') fail('Verify update', updBranch);
  if (updBranch.openingTime !== '10:00') fail('Verify openingTime', updBranch);
  pass('Verify update persisted', `name="${updBranch.name}" openingTime="${updBranch.openingTime}"`);

  // 2f. DELETE (deactivate) branch
  const dBr = await req('/branches/' + brId, 'DELETE', null, tok);
  if (dBr.status !== 200) fail('DELETE /branches', dBr.data);
  pass('DELETE /branches', dBr.data.data.message);

  // 2g. Verify deactivation
  const br4 = await req('/branches', 'GET', null, tok);
  const deactivated = br4.data.data.find(b => b.id === brId);
  if (!deactivated || deactivated.status !== 'Inactive') fail('Verify deactivation', deactivated);
  pass('Verify branch deactivated', `status="${deactivated.status}"`);

  // ═══ STEP 3: OPERATORS CRUD ═══
  console.log('\n--- OPERATORS ---');

  // 3a. GET operators
  const ops = await req('/operators', 'GET', null, tok);
  const initOpCount = ops.data.data.length;
  pass(`GET /operators`, `count: ${initOpCount}`);

  // 3b. CREATE operator
  const cOp = await req('/operators', 'POST', {
    fullName: 'E2E Test Op', username: 'e2e_op_99', password: 'SecurePass!',
    branchId: bid, dashboardPermissions: JSON.stringify({ main_dashboard: true, billing_counter: false, sessions: true })
  }, tok);
  if (!cOp.data?.data?.id) fail('POST /operators', cOp.data);
  const opId = cOp.data.data.id;
  pass('POST /operators', `id: ${opId}`);

  // 3c. Verify count
  const ops2 = await req('/operators', 'GET', null, tok);
  if (ops2.data.data.length !== initOpCount + 1) fail('Op count', { expected: initOpCount + 1, got: ops2.data.data.length });
  pass('Operator count incremented', `${initOpCount} → ${ops2.data.data.length}`);

  // 3d. UPDATE operator (change permissions, no password change)
  const uOp = await req('/operators/' + opId, 'PUT', {
    fullName: 'E2E Op RENAMED', username: 'e2e_op_99', password: '',
    branchId: bid, dashboardPermissions: JSON.stringify({ main_dashboard: true, billing_counter: true, sessions: true })
  }, tok);
  if (uOp.status !== 200) fail('PUT /operators', uOp.data);
  pass('PUT /operators (no password change)', `username: ${uOp.data.data.username}`);

  // 3e. Verify the operator can still login with old password (password wasn't changed)
  const opLogin = await req('/auth/operator/login', 'POST', { branchId: bid, username: 'e2e_op_99', password: 'SecurePass!' });
  if (opLogin.status !== 200) fail('Operator login after no-password update', opLogin.data);
  pass('Operator login still works after update (password preserved)');

  // 3f. UPDATE operator WITH password change
  const uOp2 = await req('/operators/' + opId, 'PUT', {
    fullName: 'E2E Op RENAMED', username: 'e2e_op_99', password: 'NewPass123!',
    branchId: bid, dashboardPermissions: JSON.stringify({ main_dashboard: true, billing_counter: true, sessions: true })
  }, tok);
  if (uOp2.status !== 200) fail('PUT /operators (with password)', uOp2.data);
  pass('PUT /operators (with password change)');

  // 3g. Verify old password fails
  const opLoginOld = await req('/auth/operator/login', 'POST', { branchId: bid, username: 'e2e_op_99', password: 'SecurePass!' });
  if (opLoginOld.status === 200) fail('Old password should have been rejected', opLoginOld.data);
  pass('Old password correctly rejected');

  // 3h. Verify new password works
  const opLoginNew = await req('/auth/operator/login', 'POST', { branchId: bid, username: 'e2e_op_99', password: 'NewPass123!' });
  if (opLoginNew.status !== 200) fail('New password login', opLoginNew.data);
  pass('New password login works');

  // 3i. Verify permissions persisted in GET response
  const ops3 = await req('/operators', 'GET', null, tok);
  const updatedOp = ops3.data.data.find(o => o.id === opId);
  if (!updatedOp) fail('Find updated operator', ops3.data.data);
  const perms = JSON.parse(updatedOp.dashboardPermissions);
  if (!perms.billing_counter) fail('Permissions not updated', perms);
  pass('Permissions verified', `billing_counter=${perms.billing_counter}`);

  // 3j. DELETE (disable) operator
  const dOp = await req('/operators/' + opId, 'DELETE', null, tok);
  if (dOp.status !== 200) fail('DELETE /operators', dOp.data);
  pass('DELETE /operators', dOp.data.data.message);

  // ═══ STEP 4: PC FLEET CRUD ═══
  console.log('\n--- PC FLEET ---');

  // 4a. GET PCs via /pcs/details (the new detailed endpoint)
  const pcDet = await req('/pcs/details?branchId=' + bid, 'GET', null, tok);
  if (!pcDet.data?.data) fail('GET /pcs/details', pcDet.data);
  const initPcCount = pcDet.data.data.length;
  pass(`GET /pcs/details`, `count: ${initPcCount}`);

  // 4b. Verify details endpoint returns full fields
  if (initPcCount > 0) {
    const firstPc = pcDet.data.data[0];
    const hasFields = 'pcNumber' in firstPc && 'pcName' in firstPc && 'specs' in firstPc && 'zone' in firstPc && 'hardwareNotes' in firstPc;
    if (!hasFields) fail('PC details missing fields', Object.keys(firstPc));
    pass('PC details has all required fields', `pcNumber="${firstPc.pcNumber}" zone="${firstPc.zone}"`);
  }

  // 4c. Verify old /pcs endpoint is still working (doesn't have details)
  const pcOld = await req('/pcs?branchId=' + bid, 'GET', null, tok);
  if (pcOld.status !== 200) fail('GET /pcs (old)', pcOld.data);
  pass('GET /pcs (legacy) still works', `count: ${pcOld.data.data.length}`);

  // 4d. CREATE PC
  const specs = JSON.stringify({ gpu: 'RTX 4090', cpu: 'i9-14900K', ram: '64GB' });
  const cPc = await req('/pcs', 'POST', {
    pcNumber: 'PC-E2E-777', pcName: 'E2E Test Rig', branchId: bid,
    ipAddress: '10.0.0.77', specs, zone: 'VIP', hardwareNotes: 'E2E test unit'
  }, tok);
  if (!cPc.data?.data?.id) fail('POST /pcs', cPc.data);
  const pcId = cPc.data.data.id;
  pass('POST /pcs', `id: ${pcId}`);

  // 4e. Verify it shows in /pcs/details with correct data
  const pcDet2 = await req('/pcs/details?branchId=' + bid, 'GET', null, tok);
  const newPc = pcDet2.data.data.find(p => p.id === pcId);
  if (!newPc) fail('Find new PC in details', pcDet2.data.data.map(p => p.id));
  if (newPc.pcNumber !== 'PC-E2E-777') fail('PC number mismatch', newPc);
  if (newPc.pcName !== 'E2E Test Rig') fail('PC name mismatch', newPc);
  if (newPc.zone !== 'VIP') fail('PC zone mismatch', newPc);
  if (newPc.hardwareNotes !== 'E2E test unit') fail('PC hardwareNotes mismatch', newPc);
  const newSpecs = typeof newPc.specs === 'string' ? JSON.parse(newPc.specs) : newPc.specs;
  if (newSpecs.gpu !== 'RTX 4090') fail('PC specs GPU mismatch', newSpecs);
  pass('Verify PC created with all fields', `num="${newPc.pcNumber}" name="${newPc.pcName}" zone="${newPc.zone}" gpu="${newSpecs.gpu}"`);

  // 4f. Uniqueness guard
  const dupPc = await req('/pcs', 'POST', {
    pcNumber: 'PC-E2E-777', pcName: 'Dup', branchId: bid, ipAddress: '10.0.0.1', specs: '{}', zone: 'Standard'
  }, tok);
  if (dupPc.data?.data?.id) fail('Uniqueness guard should reject', dupPc.data);
  pass('PC uniqueness guard', `error: "${dupPc.data.error}"`);

  // 4g. UPDATE PC
  const updSpecs = JSON.stringify({ gpu: 'RTX 5090', cpu: 'i9-15900K', ram: '128GB' });
  const uPc = await req('/pcs/' + pcId, 'PUT', {
    pcNumber: 'PC-E2E-777-MOD', pcName: 'E2E Rig UPGRADED', ipAddress: '10.0.0.78',
    specs: updSpecs, zone: 'Premium', hardwareNotes: 'Upgraded E2E unit'
  }, tok);
  if (uPc.status !== 200) fail('PUT /pcs', uPc.data);
  pass('PUT /pcs', `pcNumber: ${uPc.data.data.pcNumber}`);

  // 4h. Verify update persisted in /pcs/details
  const pcDet3 = await req('/pcs/details?branchId=' + bid, 'GET', null, tok);
  const updPc = pcDet3.data.data.find(p => p.id === pcId);
  if (!updPc) fail('Find updated PC', pcDet3.data.data.map(p => p.id));
  if (updPc.pcNumber !== 'PC-E2E-777-MOD') fail('Updated pcNumber', updPc);
  if (updPc.pcName !== 'E2E Rig UPGRADED') fail('Updated pcName', updPc);
  if (updPc.zone !== 'Premium') fail('Updated zone', updPc);
  if (updPc.hardwareNotes !== 'Upgraded E2E unit') fail('Updated hardwareNotes', updPc);
  const updPcSpecs = typeof updPc.specs === 'string' ? JSON.parse(updPc.specs) : updPc.specs;
  if (updPcSpecs.gpu !== 'RTX 5090') fail('Updated specs GPU', updPcSpecs);
  if (updPcSpecs.ram !== '128GB') fail('Updated specs RAM', updPcSpecs);
  pass('Verify update persisted with ALL fields', `num="${updPc.pcNumber}" name="${updPc.pcName}" zone="${updPc.zone}" gpu="${updPcSpecs.gpu}" ram="${updPcSpecs.ram}"`);

  // 4i. DELETE (soft) PC
  const dPc = await req('/pcs/' + pcId, 'DELETE', null, tok);
  if (dPc.status !== 200) fail('DELETE /pcs', dPc.data);
  pass('DELETE /pcs', dPc.data.data.message);

  // 4j. Verify PC no longer shows in /pcs/details
  const pcDet4 = await req('/pcs/details?branchId=' + bid, 'GET', null, tok);
  const deletedPc = pcDet4.data.data.find(p => p.id === pcId);
  if (deletedPc) fail('Deleted PC should not appear', deletedPc);
  pass('Deleted PC hidden from listing', `count back to: ${pcDet4.data.data.length}`);

  // ═══ STEP 5: AUDIT LOGS ═══
  console.log('\n--- AUDIT LOGS ---');
  const auditAll = await req('/audit-logs', 'GET', null, tok);
  pass(`GET /audit-logs`, `count: ${auditAll.data.data.length}`);

  const auditBranch = await req('/audit-logs/branch?branchId=' + bid, 'GET', null, tok);
  pass(`GET /audit-logs/branch`, `count: ${auditBranch.data.data.length}`);

  // ═══ FINAL SUMMARY ═══
  console.log('\n========================================================================');
  console.log('  ✅ ALL 30+ TEST SCENARIOS PASSED — ZERO FAILURES');
  console.log('========================================================================\n');
}

run().catch(e => {
  console.error('FATAL:', e.message, e.stack);
  process.exit(1);
});
