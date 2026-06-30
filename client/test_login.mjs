async function test() {
  const res = await fetch('http://appleesports-api:8080/api/auth/operator/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      branchId: 'b0000000-0000-0000-0000-000000000002', // City Light
      username: 'nazmin',
      password: '1234'
    })
  });
  console.log('Status:', res.status);
  console.log('Response:', await res.json());
}
test();
