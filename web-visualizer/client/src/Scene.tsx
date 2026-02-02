import { useRef, useState, useCallback } from "react";
import { Canvas } from "@react-three/fiber";
import { OrbitControls } from "@react-three/drei";
import type { WorldState } from "@web-visualizer/shared";
import { Chunks } from "./Chunks";
import { useAtlasTexture } from "./useAtlasTexture";

interface SceneProps {
  worldState: WorldState | null;
}

/** Camera distance so the stream region fits when zoomed out (9×9 chunks = 144 units). */
const CAMERA_DISTANCE = 200;

/** Default AR: scale and position (user can adjust via Adjust panel). */
const AR_DEFAULT_SCALE = 0.004;
const AR_DEFAULT_POSITION: [number, number, number] = [0, 0.75, -1.2];

const SCALE_MIN = 0.0015;
const SCALE_MAX = 0.012;
const POS_MIN = -1.5;
const POS_MAX = 1.5;

function SceneContent({
  worldState,
  xrActive,
  arScale,
  arPosition,
}: {
  worldState: WorldState;
  xrActive: boolean;
  arScale: number;
  arPosition: [number, number, number];
}) {
  const atlasTexture = useAtlasTexture();
  const content = <Chunks worldState={worldState} atlasTexture={atlasTexture} />;
  if (xrActive) {
    return (
      <group position={arPosition} scale={[arScale, arScale, arScale]}>
        {content}
      </group>
    );
  }
  return content;
}

export function Scene({ worldState }: SceneProps) {
  const glRef = useRef<THREE.WebGLRenderer | null>(null);
  const sessionRef = useRef<XRSession | null>(null);
  const [xrActive, setXrActive] = useState(false);
  const [adjustOpen, setAdjustOpen] = useState(false);
  const [arScale, setArScale] = useState(AR_DEFAULT_SCALE);
  const [arPosition, setArPosition] = useState<[number, number, number]>([...AR_DEFAULT_POSITION]);

  const resetARPlacement = useCallback(() => {
    setArScale(AR_DEFAULT_SCALE);
    setArPosition([...AR_DEFAULT_POSITION]);
  }, []);

  const exitAR = useCallback(() => {
    sessionRef.current?.end().catch(() => {});
    sessionRef.current = null;
  }, []);

  const enterAR = useCallback(async () => {
    if (xrActive) {
      exitAR();
      return;
    }
    if (typeof navigator === "undefined" || !navigator.xr) {
      alert("WebXR not available. Use Quest browser or Chrome for Android.");
      return;
    }
    try {
      const session = await navigator.xr.requestSession("immersive-ar", {
        optionalFeatures: ["local-floor"],
      });
      sessionRef.current = session;
      const gl = glRef.current;
      if (!gl?.xr) {
        session.end();
        return;
      }
      gl.xr.setSession(session);
      gl.setClearColor(0x000000, 0);
      setXrActive(true);
      session.addEventListener("end", () => {
        sessionRef.current = null;
        gl.setClearColor(0x1a1a2e, 1);
        setXrActive(false);
      });
    } catch (e) {
      console.warn("WebXR AR session failed", e);
      alert("Could not start AR. Try the Quest browser and allow permissions.");
    }
  }, [xrActive, exitAR]);

  return (
    <div style={{ position: "relative", width: "100%", height: "100%" }}>
      <Canvas
        camera={{ position: [CAMERA_DISTANCE, CAMERA_DISTANCE, CAMERA_DISTANCE], fov: 50 }}
        gl={{ antialias: true, xrCompatible: true }}
        style={{ width: "100%", height: "100%" }}
        onCreated={({ gl }) => {
          gl.xr.enabled = true;
          glRef.current = gl;
        }}
      >
        <color attach="background" args={["#1a1a2e"]} />
        <ambientLight intensity={0.6} />
        <directionalLight position={[40, 60, 40]} intensity={0.8} castShadow />
        {!xrActive && <OrbitControls makeDefault enableDamping dampingFactor={0.05} />}
        {worldState && <SceneContent worldState={worldState} xrActive={xrActive} />}
      </Canvas>
      <div style={{ position: "absolute", bottom: 16, right: 16, display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 8, pointerEvents: "auto" }}>
        {xrActive && (
          <>
            <button
              type="button"
              onClick={() => setAdjustOpen((o) => !o)}
              style={btnStyle}
              title="Adjust scale and position"
            >
              {adjustOpen ? "Done" : "Adjust"}
            </button>
            {adjustOpen && (
              <div style={panelStyle}>
                <div style={panelTitle}>Scale &amp; position</div>
                <label style={labelStyle}>
                  Scale
                  <input
                    type="range"
                    min={SCALE_MIN}
                    max={SCALE_MAX}
                    step={0.0005}
                    value={arScale}
                    onChange={(e) => setArScale(Number(e.target.value))}
                    style={sliderStyle}
                  />
                  <span style={valueStyle}>{(arScale * 1000).toFixed(1)}</span>
                </label>
                <label style={labelStyle}>
                  X
                  <input
                    type="range"
                    min={POS_MIN}
                    max={POS_MAX}
                    step={0.1}
                    value={arPosition[0]}
                    onChange={(e) => setArPosition((p) => [Number(e.target.value), p[1], p[2]])}
                    style={sliderStyle}
                  />
                </label>
                <label style={labelStyle}>
                  Y
                  <input
                    type="range"
                    min={POS_MIN}
                    max={POS_MAX}
                    step={0.1}
                    value={arPosition[1]}
                    onChange={(e) => setArPosition((p) => [p[0], Number(e.target.value), p[2]])}
                    style={sliderStyle}
                  />
                </label>
                <label style={labelStyle}>
                  Z
                  <input
                    type="range"
                    min={POS_MIN}
                    max={POS_MAX}
                    step={0.1}
                    value={arPosition[2]}
                    onChange={(e) => setArPosition((p) => [p[0], p[1], Number(e.target.value)])}
                    style={sliderStyle}
                  />
                </label>
                <button type="button" onClick={resetARPlacement} style={{ ...btnStyle, marginTop: 4 }}>
                  Reset
                </button>
              </div>
            )}
          </>
        )}
        <button
          type="button"
          onClick={enterAR}
          style={{
            ...btnStyle,
            background: xrActive ? "#2a2" : "#333",
          }}
        >
          {xrActive ? "Exit AR" : "Enter AR"}
        </button>
      </div>
    </div>
  );
}

const btnStyle: React.CSSProperties = {
  padding: "10px 16px",
  fontSize: 14,
  fontWeight: 600,
  background: "#333",
  color: "#fff",
  border: "none",
  borderRadius: 8,
  cursor: "pointer",
  boxShadow: "0 2px 8px rgba(0,0,0,0.3)",
};

const panelStyle: React.CSSProperties = {
  background: "rgba(0,0,0,0.85)",
  color: "#eee",
  padding: 12,
  borderRadius: 8,
  minWidth: 200,
  boxShadow: "0 2px 12px rgba(0,0,0,0.5)",
};

const panelTitle: React.CSSProperties = { fontSize: 12, fontWeight: 600, marginBottom: 8 };

const labelStyle: React.CSSProperties = { display: "block", fontSize: 11, marginBottom: 4 };

const sliderStyle: React.CSSProperties = { width: "100%", marginLeft: 4 };

const valueStyle: React.CSSProperties = { marginLeft: 6, fontSize: 11, color: "#aaa" };
