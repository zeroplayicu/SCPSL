/**
 * make-test-icon.js —— 生成一张 48×48 的 PNG 测试图标（GOC 风格：青色圆底 + 白色骷髅眼嘴）
 * 用于验证 img2hint.js 的转换链路。
 */
const fs = require('fs');
const zlib = require('zlib');

// CRC32
const crcTable = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
    t[n] = c;
  }
  return t;
})();
function crc32(buf) {
  let c = 0xFFFFFFFF;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xFF] ^ (c >>> 8);
  return (c ^ 0xFFFFFFFF) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length, 0);
  const t = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(Buffer.concat([t, data])), 0);
  return Buffer.concat([len, t, data, crc]);
}

const W = 48, H = 48;
const px = Buffer.alloc(W * H * 4);   // RGBA

function set(x, y, r, g, b, a) {
  if (x < 0 || y < 0 || x >= W || y >= H) return;
  const i = (y * W + x) * 4;
  px[i] = r; px[i + 1] = g; px[i + 2] = b; px[i + 3] = a;
}
function fillCircle(cx, cy, rad, r, g, b, a) {
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const dx = x - cx, dy = y - cy;
    if (dx * dx + dy * dy <= rad * rad) set(x, y, r, g, b, a);
  }
}
function fillRect(x0, y0, w, h, r, g, b, a) {
  for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) set(x, y, r, g, b, a);
}

// 青色圆底（GOC 主色 #00AEEF）
fillCircle(24, 24, 22, 0x00, 0xAE, 0xEF, 255);
// 白色骷髅：两眼 + 鼻孔 + 嘴
fillCircle(16, 20, 6, 255, 255, 255, 255);
fillCircle(32, 20, 6, 255, 255, 255, 255);
fillCircle(24, 28, 3, 255, 255, 255, 255);
fillRect(16, 34, 16, 3, 255, 255, 255, 255);
fillRect(19, 37, 3, 4, 255, 255, 255, 255);
fillRect(26, 37, 3, 4, 255, 255, 255, 255);

// 扫描线（每行前置 filter=0）
const raw = Buffer.alloc(H * (W * 4 + 1));
for (let y = 0; y < H; y++) {
  raw[y * (W * 4 + 1)] = 0;
  px.copy(raw, y * (W * 4 + 1) + 1, y * W * 4, (y + 1) * W * 4);
}

const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(W, 0);
ihdr.writeUInt32BE(H, 4);
ihdr[8] = 8;    // bit depth
ihdr[9] = 6;    // color type RGBA
ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;

const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
  chunk('IHDR', ihdr),
  chunk('IDAT', zlib.deflateSync(raw)),
  chunk('IEND', Buffer.alloc(0))
]);

const out = process.argv[2] || 'tools/test_icon.png';
fs.writeFileSync(out, png);
console.log('已生成测试图标: ' + out + ' (' + png.length + ' 字节, ' + W + '×' + H + ')');
