// mosaic.js
// Faithful port of Telegram's album/collage tiling — mirrors
// MosaicAlbumLayout.chatMessageBubbleMosaicLayout + MessageAlbumBase.GetPositionsForWidth
// in the native app (Telegram/Common/AlbumLayout.cs, Td/Api/MessageAlbum.cs).
//
// mosaicBase(sizes)  -> base layout at MAX_WIDTH/MAX_HEIGHT (spacing 0)
// scaleMosaic(base,w) -> rects scaled to width w, with the 1px inter-item gaps
//
// Position is a bitmask matching MosaicItemPosition: Top=1 Bottom=2 Left=4 Right=8 Inside=16.

export const MAX_WIDTH = 432;
export const MAX_HEIGHT = 432;

const POS = { None: 0, Top: 1, Bottom: 2, Left: 4, Right: 8, Inside: 16 };

const round = Math.round, floor = Math.floor, ceil = Math.ceil;
const min = Math.min, max = Math.max, abs = Math.abs;

// sizes: [{ width, height }] -> ({ rects:[{x,y,width,height,position}], width, height })
export function mosaicBase(sizes, availableWidth = MAX_WIDTH, availableHeight = MAX_HEIGHT) {
  const spacing = 0;

  let proportions = "";
  let averageAspectRatio = 1.0;
  let forceCalc = false;

  const items = sizes.map((s) => {
    const aspectRatio = s.width / s.height;
    if (aspectRatio > 1.2) proportions += "w";
    else if (aspectRatio < 0.8) proportions += "n";
    else proportions += "q";
    if (aspectRatio > 2.0) forceCalc = true;
    averageAspectRatio += aspectRatio;
    return { aspectRatio, frame: { x: 0, y: 0, width: 0, height: 0 }, position: POS.None };
  });

  const minWidth = 68.0;
  const minHeight = 81.0;
  const maxAspectRatio = availableWidth / availableHeight;
  if (items.length > 0) averageAspectRatio /= items.length;

  const a = items; // shorthand

  if (!forceCalc) {
    if (a.length === 2) {
      if (proportions === "ww" && averageAspectRatio > 1.4 * maxAspectRatio && a[1].aspectRatio - a[0].aspectRatio < 0.2) {
        const width = availableWidth;
        const height = floor(min(width / a[0].aspectRatio, min(width / a[1].aspectRatio, (availableHeight - spacing) / 2.0)));
        a[0].frame = { x: 0, y: 0, width, height };
        a[0].position = POS.Top | POS.Left | POS.Right;
        a[1].frame = { x: 0, y: height + spacing, width, height };
        a[1].position = POS.Bottom | POS.Left | POS.Right;
      } else if (proportions === "ww" || proportions === "qq") {
        const width = (availableWidth - spacing) / 2.0;
        const height = floor(min(width / a[0].aspectRatio, min(width / a[1].aspectRatio, availableHeight)));
        a[0].frame = { x: 0, y: 0, width, height };
        a[0].position = POS.Top | POS.Left | POS.Bottom;
        a[1].frame = { x: width + spacing, y: 0, width, height };
        a[1].position = POS.Top | POS.Right | POS.Bottom;
      } else {
        const secondWidth = floor(min(0.5 * (availableWidth - spacing), round((availableWidth - spacing) / a[0].aspectRatio / (1.0 / a[0].aspectRatio + 1.0 / a[1].aspectRatio))));
        const firstWidth = availableWidth - secondWidth - spacing;
        const height = floor(min(availableHeight, round(min(firstWidth / a[0].aspectRatio, secondWidth / a[1].aspectRatio))));
        a[0].frame = { x: 0, y: 0, width: firstWidth, height };
        a[0].position = POS.Top | POS.Left | POS.Bottom;
        a[1].frame = { x: firstWidth + spacing, y: 0, width: secondWidth, height };
        a[1].position = POS.Top | POS.Right | POS.Bottom;
      }
    } else if (a.length === 3) {
      if (proportions.startsWith("n")) {
        const firstHeight = availableHeight;
        const thirdHeight = min((availableHeight - spacing) * 0.5, round(a[1].aspectRatio * (availableWidth - spacing) / (a[2].aspectRatio + a[1].aspectRatio)));
        const secondHeight = availableHeight - thirdHeight - spacing;
        const rightWidth = max(minWidth, min((availableWidth - spacing) * 0.5, round(min(thirdHeight * a[2].aspectRatio, secondHeight * a[1].aspectRatio))));
        const leftWidth = round(min(firstHeight * a[0].aspectRatio, availableWidth - spacing - rightWidth));
        a[0].frame = { x: 0, y: 0, width: leftWidth, height: firstHeight };
        a[0].position = POS.Top | POS.Left | POS.Bottom;
        a[1].frame = { x: leftWidth + spacing, y: 0, width: rightWidth, height: secondHeight };
        a[1].position = POS.Right | POS.Top;
        a[2].frame = { x: leftWidth + spacing, y: secondHeight + spacing, width: rightWidth, height: thirdHeight };
        a[2].position = POS.Right | POS.Bottom;
      } else {
        let width = availableWidth;
        const firstHeight = floor(min(width / a[0].aspectRatio, (availableHeight - spacing) * 0.66));
        a[0].frame = { x: 0, y: 0, width, height: firstHeight };
        a[0].position = POS.Top | POS.Left | POS.Right;
        width = (availableWidth - spacing) / 2.0;
        const secondHeight = min(availableHeight - firstHeight - spacing, round(min(width / a[1].aspectRatio, width / a[2].aspectRatio)));
        a[1].frame = { x: 0, y: firstHeight + spacing, width, height: secondHeight };
        a[1].position = POS.Left | POS.Bottom;
        a[2].frame = { x: width + spacing, y: firstHeight + spacing, width, height: secondHeight };
        a[2].position = POS.Right | POS.Bottom;
      }
    } else if (a.length === 4) {
      if (proportions === "wwww" || proportions.startsWith("w")) {
        const w = availableWidth;
        const h0 = round(min(w / a[0].aspectRatio, (availableHeight - spacing) * 0.66));
        a[0].frame = { x: 0, y: 0, width: w, height: h0 };
        a[0].position = POS.Top | POS.Left | POS.Right;
        let h = round((availableWidth - 2 * spacing) / (a[1].aspectRatio + a[2].aspectRatio + a[3].aspectRatio));
        const w0 = max(minWidth, min((availableWidth - 2 * spacing) * 0.4, h * a[1].aspectRatio));
        const w2 = max(max(minWidth, (availableWidth - 2 * spacing) * 0.33), h * a[3].aspectRatio);
        const w1 = w - w0 - w2 - 2 * spacing;
        h = max(minHeight, min(availableHeight - h0 - spacing, h));
        a[1].frame = { x: 0, y: h0 + spacing, width: w0, height: h };
        a[1].position = POS.Left | POS.Bottom;
        a[2].frame = { x: w0 + spacing, y: h0 + spacing, width: w1, height: h };
        a[2].position = POS.Bottom;
        a[3].frame = { x: w0 + w1 + 2 * spacing, y: h0 + spacing, width: w2, height: h };
        a[3].position = POS.Right | POS.Bottom;
      } else {
        const h = availableHeight;
        const w0 = round(min(h * a[0].aspectRatio, (availableWidth - spacing) * 0.6));
        a[0].frame = { x: 0, y: 0, width: w0, height: h };
        a[0].position = POS.Top | POS.Left | POS.Bottom;
        let w = round((availableHeight - 2 * spacing) / (1.0 / a[1].aspectRatio + 1.0 / a[2].aspectRatio + 1.0 / a[3].aspectRatio));
        const h0 = floor(w / a[1].aspectRatio);
        const h1 = floor(w / a[2].aspectRatio);
        const h2 = h - h0 - h1 - 2.0 * spacing;
        w = max(minWidth, min(availableWidth - w0 - spacing, w));
        a[1].frame = { x: w0 + spacing, y: 0, width: w, height: h0 };
        a[1].position = POS.Right | POS.Top;
        a[2].frame = { x: w0 + spacing, y: h0 + spacing, width: w, height: h1 };
        a[2].position = POS.Right;
        a[3].frame = { x: w0 + spacing, y: h0 + h1 + 2 * spacing, width: w, height: h2 };
        a[3].position = POS.Right | POS.Bottom;
      }
    }
  }

  if (forceCalc || a.length >= 5) {
    const croppedRatios = items.map((it) => {
      let croppedRatio = averageAspectRatio > 1.1 ? max(1.0, it.aspectRatio) : min(1.0, it.aspectRatio);
      return max(0.66667, min(1.7, croppedRatio));
    });

    const multiHeight = (ratios) => {
      let count = 0, ratioSum = 0;
      for (const r of ratios) { count++; ratioSum += r; }
      return (availableWidth - (count - 1) * spacing) / ratioSum;
    };
    const slice = (start, len) => croppedRatios.slice(start, start + len);

    const attempts = [];
    const addAttempt = (lineCounts, heights) => attempts.push({ lineCounts, heights });
    const n = croppedRatios.length;

    for (let firstLine = 1; firstLine < n; firstLine++) {
      const secondLine = n - firstLine;
      if (firstLine > 3 || secondLine > 3) continue;
      addAttempt([firstLine, n - firstLine], [
        multiHeight(slice(0, firstLine)),
        multiHeight(slice(firstLine, n - firstLine)),
      ]);
    }

    for (let firstLine = 1; firstLine < n - 1; firstLine++) {
      for (let secondLine = 1; secondLine < n - firstLine; secondLine++) {
        const thirdLine = n - firstLine - secondLine;
        if (firstLine > 3 || secondLine > (averageAspectRatio < 0.85 ? 4 : 3) || thirdLine > 3) continue;
        addAttempt([firstLine, secondLine, thirdLine], [
          multiHeight(slice(0, firstLine)),
          multiHeight(slice(firstLine, n - firstLine - thirdLine)),
          multiHeight(slice(firstLine + secondLine, n - firstLine - secondLine)),
        ]);
      }
    }

    if (n - 2 >= 1) {
      for (let firstLine = 1; firstLine < n - 2; firstLine++) {
        for (let secondLine = 1; secondLine < n - firstLine; secondLine++) {
          for (let thirdLine = 1; thirdLine < n - firstLine - secondLine; thirdLine++) {
            const fourthLine = n - firstLine - secondLine - thirdLine;
            if (firstLine > 3 || secondLine > 3 || thirdLine > 3 || fourthLine > 3) continue;
            addAttempt([firstLine, secondLine, thirdLine, fourthLine], [
              multiHeight(slice(0, firstLine)),
              multiHeight(slice(firstLine, n - firstLine - thirdLine - fourthLine)),
              multiHeight(slice(firstLine + secondLine, n - firstLine - secondLine - fourthLine)),
              multiHeight(slice(firstLine + secondLine + thirdLine, n - firstLine - secondLine - thirdLine)),
            ]);
          }
        }
      }
    }

    const maxHeight = floor(availableWidth / 3.0 * 4.0);
    let optimal = null, optimalDiff = 0;
    for (const attempt of attempts) {
      let totalHeight = spacing * (attempt.heights.length - 1);
      let minLineHeight = Number.MAX_VALUE, maxLineHeight = 0;
      for (const h of attempt.heights) {
        totalHeight += floor(h);
        if (totalHeight < minLineHeight) minLineHeight = totalHeight;
        if (totalHeight > maxLineHeight) maxLineHeight = totalHeight;
      }
      let diff = abs(totalHeight - maxHeight);
      const lc = attempt.lineCounts;
      if (lc.length > 1) {
        if ((lc[0] > lc[1]) || (lc.length > 2 && lc[1] > lc[2]) || (lc.length > 3 && lc[2] > lc[3])) diff *= 1.5;
      }
      if (minLineHeight < minWidth) diff *= 1.5;
      if (optimal == null || diff < optimalDiff) { optimal = attempt; optimalDiff = diff; }
    }

    if (optimal) {
      let index = 0, y = 0;
      for (let i = 0; i < optimal.lineCounts.length; i++) {
        const count = optimal.lineCounts[i];
        const lineHeight = ceil(optimal.heights[i]);
        let x = 0;
        let positionFlags = POS.None;
        if (i === 0) positionFlags |= POS.Top;
        if (i === optimal.lineCounts.length - 1) positionFlags |= POS.Bottom;
        for (let k = 0; k < count; k++) {
          let inner = positionFlags;
          if (k === 0) inner |= POS.Left;
          if (k === count - 1) inner |= POS.Right;
          if (inner === POS.None) inner = POS.Inside;
          const ratio = croppedRatios[index];
          const width = ceil(ratio * lineHeight);
          items[index].frame = { x, y, width, height: lineHeight };
          items[index].position = inner;
          x += width + spacing;
          index += 1;
        }
        y += lineHeight + spacing;
      }

      // last column in each line stretches to a common max right edge
      index = 0;
      let maxRight = 0;
      for (let i = 0; i < optimal.lineCounts.length; i++) {
        const count = optimal.lineCounts[i];
        for (let k = 0; k < count; k++) {
          if (k === count - 1) maxRight = max(maxRight, items[index].frame.x + items[index].frame.width);
          index += 1;
        }
      }
      index = 0;
      for (let i = 0; i < optimal.lineCounts.length; i++) {
        const count = optimal.lineCounts[i];
        for (let k = 0; k < count; k++) {
          if (k === count - 1) {
            const f = items[index].frame;
            f.width = max(f.width, maxRight - f.x);
          }
          index += 1;
        }
      }
    }
  }

  let width = 0, height = 0;
  for (const it of items) {
    width = max(width, round(it.frame.x + it.frame.width));
    height = max(height, round(it.frame.y + it.frame.height));
  }

  return {
    rects: items.map((it) => ({ ...it.frame, position: it.position })),
    width,
    height,
  };
}

const sanitize = (v) => {
  v = max(0, v);
  if (Number.isNaN(v) || !Number.isFinite(v)) v = 0;
  return v;
};

// Scale a base layout to width w, inserting the 1px gaps between adjacent items
// (top/left edges keep their position, inner edges shrink by 1px). Mirrors
// MessageAlbumBase.GetPositionsForWidth.
export function scaleMosaic(base, w) {
  const ratio = w / base.width;
  const rects = base.rects.map((r) => {
    let x = sanitize(r.x * ratio);
    let y = sanitize(r.y * ratio);
    let width = sanitize(r.width * ratio);
    let height = sanitize(r.height * ratio);
    if (height >= 1 && !(r.position & POS.Top)) { y += 1; height -= 1; }
    if (width >= 1 && !(r.position & POS.Left)) { x += 1; width -= 1; }
    return { x, y, width, height };
  });
  return { rects, width: sanitize(base.width * ratio), height: sanitize(base.height * ratio) };
}
