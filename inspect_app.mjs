import { chromium } from 'playwright';

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

const consoleMessages = [];
const errors = [];

page.on('console', msg => {
  consoleMessages.push({ type: msg.type(), text: msg.text() });
});

page.on('pageerror', err => {
  errors.push(err.message + '\n' + err.stack);
});

page.on('requestfailed', req => {
  errors.push(`NETWORK FAIL: ${req.method()} ${req.url()} — ${req.failure()?.errorText}`);
});

try {
  await page.goto('http://localhost:5173', { waitUntil: 'networkidle', timeout: 15000 });
} catch(e) {
  errors.push('Navigation error: ' + e.message);
}

await page.waitForTimeout(3000);

await page.screenshot({ path: 'C:/Users/harsh/Desktop/meet gamiing software/screenshot_debug.png', fullPage: true });

const rootContent = await page.evaluate(() => document.getElementById('root')?.innerHTML || 'ROOT NOT FOUND');
const bodyText = await page.evaluate(() => document.body.innerText);

console.log('=== PAGE ERRORS ===');
errors.forEach(e => console.log(e));

console.log('\n=== CONSOLE MESSAGES ===');
consoleMessages.forEach(m => console.log(`[${m.type}] ${m.text}`));

console.log('\n=== #root innerHTML (first 800 chars) ===');
console.log(rootContent.substring(0, 800));

console.log('\n=== BODY VISIBLE TEXT ===');
console.log(bodyText.substring(0, 400));

await browser.close();
