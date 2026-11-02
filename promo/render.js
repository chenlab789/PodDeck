// Render motion.html to PNG frames with headless Chrome, then (optionally) mp4 via ffmpeg.
// node render.js <w> <h> <part> <outDir> [--preview t1,t2,...] [--fps 30]
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const puppeteer = require('puppeteer-core');

const [w, h, part, outDir] = process.argv.slice(2, 6);
const args = process.argv.slice(6);
const opt = (k, d) => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : d; };
const fps = +opt('--fps', 30);
const preview = opt('--preview', null);
const CHROME = process.env.CHROME || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const FFMPEG = process.env.FFMPEG || 'ffmpeg';

(async () => {
  fs.mkdirSync(outDir, { recursive: true });
  const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new', args: ['--force-device-scale-factor=1'] });
  const page = await browser.newPage();
  await page.setViewport({ width: +w, height: +h, deviceScaleFactor: 1 });
  const url = 'file:///' + path.resolve(__dirname, (process.env.MOTION || 'motion.html')).replace(/\\/g, '/') + `?w=${w}&h=${h}&part=${part}&static=1`;
  await page.goto(url);
  await page.evaluate(() => document.fonts.ready);
  const total = await page.evaluate(() => window.TOTAL);
  const sfx = await page.evaluate(() => window.SFX);
  fs.writeFileSync(path.join(outDir, 'sfx.json'), JSON.stringify({ total, sfx }, null, 1));

  const times = preview ? preview.split(',').map(Number) : Array.from({ length: Math.round(total * fps) }, (_, i) => i / fps);
  for (let i = 0; i < times.length; i++) {
    const data = await page.evaluate((t) => { window.render(t); return document.getElementById('c').toDataURL('image/png'); }, times[i]);
    const name = preview ? `preview_${times[i].toFixed(2)}.png` : `f_${String(i).padStart(5, '0')}.png`;
    fs.writeFileSync(path.join(outDir, name), Buffer.from(data.split(',')[1], 'base64'));
  }
  await browser.close();

  if (!preview) {
    const mp4 = path.join(outDir, `${part}_${w}x${h}.mp4`);
    execFileSync(FFMPEG, ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', path.join(outDir, 'f_%05d.png'),
      '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '16', '-preset', 'slow', mp4]);
    for (const f of fs.readdirSync(outDir)) if (f.startsWith('f_')) fs.unlinkSync(path.join(outDir, f));
    console.log('wrote', mp4, 'total', total, 's');
  } else console.log('preview frames', times.length);
})();


