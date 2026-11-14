// The desktop's text formatting (mu_swprintf, ConvertGold) for the field events' texts.

/** swprintf for the desktop's formats: %d, %u, %lu, %s, %ls with a width (%3d) or %.2d, and %%. */
export function format(pattern: string, ...args: (string | number)[]): string {
  let next = 0;
  return pattern.replace(/%%|%(\.)?(\d+)?(l?[dus])/g, (match: string, dot: string | undefined, width: string | undefined) => {
    if (match === '%%') return '%';
    const value = args[next++];
    const text = value === undefined ? '' : String(value);
    if (!width) return text;
    return dot ? text.padStart(Number(width), '0') : text.padStart(Number(width), ' ');
  });
}

/** ConvertGold: the zen with thousands commas. */
export function gold(value: number): string {
  return Math.trunc(value).toLocaleString('en-US');
}
