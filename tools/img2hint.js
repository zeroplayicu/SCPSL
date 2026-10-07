/**
 * img2hint.js —— 把 PNG 图片转换成 SCP:SL hint 可显示的彩色文本
 *
 * 用法：node img2hint.js <input.png> [maxWidth=32] [outFile] [quantLevels=4] [whiteCut=235]
 *   quantLevels: 颜色量化级数（每通道），4 → 最多 64 色；数值越小文本越短
 *   whiteCut:    近白阈值，RGB 均高于该值视为透明（去掉白底/噪点），0 = 不处理
 *
 * 优化点（针对带噪点的截图）：
 *   1) 面积平均下采样（而非最近邻）→ 平滑噪点
 *   2) 颜色量化 → 大幅减少 <color> 标签数量
 *   3) 近白像素转透明 → 去掉背景，减少像素数
 */
const fs = require('fs');
const zlib = require('zlib');

function parsePNG(buf) {
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error('不是有效的 PNG 文件');
  let pos = 8, width = 0, height = 0, bitDepth = 8, colorType = 6;
  const idatChunks = [];
  while (pos + 8 <= buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.slice(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      if (data[12] !== 0) throw new Error('不支持交错(interlaced) PNG');
    } else if (type === 'IDAT') idatChunks.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (bitDepth !== 8) throw new Error('仅支持 8 位色深 PNG');

  const channels = colorType === 6 ? 4 : colorType === 2 ? 3 : colorType === 0 ? 1 : colorType === 4 ? 2 : 0;
  if (channels === 0) throw new Error('不支持的颜色类型: ' + colorType);

  const raw = zlib.inflateSync(Buffer.concat(idatChunks));
  const stride = width * channels;
  const out = Buffer.alloc(height * stride);
  let rp = 0;
  for (let y = 0; y < height; y++) {
    const filter = raw[rp++];
    const line = raw.slice(rp, rp + stride); rp += stride;
    const prevOff = (y - 1) * stride, curOff = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= channels ? out[curOff + x - channels] : 0;
      const b = y > 0 ? out[prevOff + x] : 0;
      const c = (x >= channels && y > 0) ? out[prevOff + x - channels] : 0;
      let v = line[x];
      switch (filter) {
        case 1: v = (v + a) & 0xff; break;
        case 2: v = (v + b) & 0xff; break;
        case 3: v = (v + ((a + b) >> 1)) & 0xff; break;
        case 4: {
          const p = a + b - c;
          const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
          v = (v + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c)) & 0xff;
          break;
        }
      }
      out[curOff + x] = v;
    }
  }
  return { width, height, channels, pixels: out };
}

function getPixel(png, x, y) {
  const i = (y * png.width + x) * png.channels, p = png.pixels;
  if (png.channels === 4) return [p[i], p[i + 1], p[i + 2], p[i + 3]];
  if (png.channels === 3) return [p[i], p[i + 1], p[i + 2], 255];
  if (png.channels === 2) return [p[i], p[i], p[i], p[i + 1]];
  return [p[i], p[i], p[i], 255];
}

/** 面积平均下采样（平滑噪点） */
function scaleAverage(png, tw, th) {
  const out = [];
  for (let y = 0; y < th; y++) {
    const row = [];
    const y0 = Math.floor(y * png.height / th), y1 = Math.max(y0 + 1, Math.floor((y + 1) * png.height / th));
    for (let x = 0; x < tw; x++) {
      const x0 = Math.floor(x * png.width / tw), x1 = Math.max(x0 + 1, Math.floor((x + 1) * png.width / tw));
      let r = 0, g = 0, b = 0, a = 0, n = 0;
      for (let yy = y0; yy < y1; yy++) for (let xx = x0; xx < x1; xx++) {
        const [pr, pg, pb, pa] = getPixel(png, xx, yy);
        r += pr; g += pg; b += pb; a += pa; n++;
      }
      row.push([Math.round(r / n), Math.round(g / n), Math.round(b / n), Math.round(a / n)]);
    }
    out.push(row);
  }
  return out;
}

function toHint(grid, tw, th, levels, whiteCut) {
  const q = v => {
    const step = 255 / (levels - 1);
    return Math.round(Math.round(v / step) * step);
  };
  let out = '', curColor = null;
  for (let y = 0; y < th; y++) {
    for (let x = 0; x < tw; x++) {
      let [r, g, b, a] = grid[y][x];
      if (a < 128) { out += ' '; continue; }
      if (whiteCut > 0 && r >= whiteCut && g >= whiteCut && b >= whiteCut) { out += ' '; continue; }
      r = q(r); g = q(g); b = q(b);
      const hex = '#' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase();
      if (hex !== curColor) {
        out += (curColor === null ? '' : '</color>') + '<color=' + hex + '>';
        curColor = hex;
      }
      out += '█';
    }
    out += '\n';
  }
  if (curColor !== null) out += '</color>';
  return out.replace(/\n<\/color>$/, '');
}

const args = process.argv.slice(2);
if (args.length < 1) {
  console.log('用法: node img2hint.js <input.png> [maxWidth=32] [outFile] [quantLevels=4] [whiteCut=235]');
  process.exit(1);
}
const input = args[0];
const maxWidth = parseInt(args[1] || '32', 10);
const outFile = args[2];
const levels = parseInt(args[3] || '4', 10);
const whiteCut = parseInt(args[4] || '235', 10);

try {
  const png = parsePNG(fs.readFileSync(input));
  const tw = Math.min(maxWidth, png.width);
  const th = Math.max(1, Math.round(png.height * tw / png.width));
  const grid = scaleAverage(png, tw, th);
  const hint = toHint(grid, tw, th, levels, whiteCut);

  const colors = new Set(hint.match(/#[0-9A-F]{6}/g) || []);
  console.log('原始: ' + png.width + '×' + png.height + ' → 输出: ' + tw + '×' + th);
  console.log('颜色数: ' + colors.size + '（量化 ' + levels + ' 级/通道）');
  console.log('hint 字符数: ' + hint.length + ' / 上限 65534');

  if (outFile) {
    fs.writeFileSync(outFile, hint, 'utf8');
    console.log('已写入: ' + outFile);
  }
} catch (e) {
  console.error('转换失败: ' + e.message);
  process.exit(1);
}
