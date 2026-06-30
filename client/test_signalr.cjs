import * as signalR from '@microsoft/signalr';

async function testConnection() {
    const hubUrl = 'http://localhost:5015/hubs/dashboard';
    // fetch token from a successful login
    const axios = require('axios');
    const response = await axios.post('http://localhost:5015/api/auth/admin/login', {
        email: 'admin@appleesports.com',
        password: 'AdminPassword123!'
    });
    const token = response.data.data.accessToken;
    console.log('Got token, length:', token.length);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, { accessTokenFactory: () => token })
        .build();

    try {
        await connection.start();
        console.log('Connected successfully!');
        await connection.stop();
    } catch (err) {
        console.error('Connection failed:', err.message);
    }
}

testConnection();
