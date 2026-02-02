/**
 * Normalize any block state string from the server into "minecraft:block_id" for atlas lookup.
 * Handles: "minecraft:dirt", "minecraft:grass_block[snowy=false]", "Block{minecraft:stone}",
 * leading/trailing whitespace, and missing namespace.
 */
export function normalizeBlockStateToBaseId(raw: string): string {
  const s = raw.trim();
  if (!s) return "minecraft:air";
  // Extract "minecraft:blockid" (id = letters, digits, underscores; stop at '[' or end)
  const minecraftMatch = s.match(/minecraft:([a-z0-9_]+)/i);
  if (minecraftMatch) return "minecraft:" + minecraftMatch[1].toLowerCase();
  // No "minecraft:" prefix: use as block id (e.g. "stone" -> "minecraft:stone")
  const noBracket = s.includes("[") ? s.slice(0, s.indexOf("[")).trim() : s;
  const id = noBracket.split(":").pop() ?? noBracket;
  return id ? "minecraft:" + id.toLowerCase() : "minecraft:stone";
}
