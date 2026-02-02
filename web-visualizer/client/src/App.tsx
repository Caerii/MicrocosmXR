import { useStream } from "./useStream";
import { Scene } from "./Scene";

// Derive from current page so it works on localhost, LAN IP, or ngrok (HTTPS → wss)
const WS_URL =
  (typeof window !== "undefined" && window.location?.host)
    ? `${window.location.protocol === "https:" ? "wss:" : "ws:"}//${window.location.host}/ws`
    : "ws://localhost:5173/ws";

export default function App() {
  const { worldState, connected, error, connect, disconnect } = useStream(WS_URL);

  return (
    <>
      <div style={overlay}>
        <div style={toolbar}>
          <span style={status(connected)}>{connected ? "Connected" : "Disconnected"}</span>
          {error && <span style={errorStyle}>{error}</span>}
          {!connected ? (
            <button type="button" onClick={connect} style={btn}>
              Connect
            </button>
          ) : (
            <button type="button" onClick={disconnect} style={btn}>
              Disconnect
            </button>
          )}
          {worldState && (
            <span style={meta}>
              Sections: {worldState.sections.size} | Origin: ({worldState.originX}, {worldState.originY},{" "}
              {worldState.originZ}) | Scale: {worldState.scale}
            </span>
          )}
        </div>
      </div>
      <Scene worldState={worldState} />
    </>
  );
}

const overlay: React.CSSProperties = {
  position: "fixed",
  top: 0,
  left: 0,
  right: 0,
  zIndex: 10,
  pointerEvents: "none",
};

const toolbar: React.CSSProperties = {
  padding: "8px 12px",
  display: "flex",
  alignItems: "center",
  gap: "12px",
  flexWrap: "wrap",
  pointerEvents: "auto",
};

const status = (connected: boolean): React.CSSProperties => ({
  fontSize: 12,
  color: connected ? "#6f6" : "#f66",
});

const errorStyle: React.CSSProperties = { fontSize: 12, color: "#f96", maxWidth: 200 };

const btn: React.CSSProperties = {
  padding: "4px 10px",
  fontSize: 12,
  cursor: "pointer",
  background: "#333",
  color: "#eee",
  border: "1px solid #555",
  borderRadius: 4,
};

const meta: React.CSSProperties = { fontSize: 11, color: "#888" };
