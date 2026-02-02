import { useMemo } from "react";
import type { WorldState } from "@web-visualizer/shared";
import * as THREE from "three";
import { buildSectionMesh } from "./sectionMesher";

interface ChunksProps {
  worldState: WorldState;
  atlasTexture: THREE.Texture;
}

function SectionMeshTextured({
  cx,
  cz,
  sy,
  palette,
  indices,
  originX,
  originY,
  originZ,
  atlasTexture,
}: {
  cx: number;
  cz: number;
  sy: number;
  palette: string[];
  indices: Uint16Array;
  originX: number;
  originY: number;
  originZ: number;
  atlasTexture: THREE.Texture;
}) {
  const geometry = useMemo(() => {
    const { position, normal, uv } = buildSectionMesh(
      palette,
      indices,
      cx,
      cz,
      sy,
      originX,
      originY,
      originZ
    );
    if (position.length === 0) return null;
    const geom = new THREE.BufferGeometry();
    geom.setAttribute("position", new THREE.BufferAttribute(position, 3));
    geom.setAttribute("normal", new THREE.BufferAttribute(normal, 3));
    geom.setAttribute("uv", new THREE.BufferAttribute(uv, 2));
    return geom;
  }, [cx, cz, sy, palette, indices, originX, originY, originZ]);

  if (!geometry) return null;

  return (
    <mesh geometry={geometry}>
      <meshLambertMaterial map={atlasTexture} side={THREE.FrontSide} />
    </mesh>
  );
}

export function Chunks({ worldState, atlasTexture }: ChunksProps) {
  const { originX, originY, originZ } = worldState;
  const entries = useMemo(
    () => Array.from(worldState.sections.entries()),
    [worldState.sections]
  );

  return (
    <group>
      {entries.map(([key, section]) => {
        const [cx, cz, sy] = key.split(",").map(Number);
        return (
          <SectionMeshTextured
            key={key}
            cx={cx}
            cz={cz}
            sy={sy}
            palette={section.palette}
            indices={section.indices}
            originX={originX}
            originY={originY}
            originZ={originZ}
            atlasTexture={atlasTexture}
          />
        );
      })}
    </group>
  );
}
