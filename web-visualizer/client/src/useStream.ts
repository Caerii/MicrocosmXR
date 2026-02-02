import { useState, useCallback, useRef, useEffect } from "react";
import {
  createWorldState,
  createStreamCallbacks,
  parseText,
  parseBinary,
  type WorldState,
} from "@web-visualizer/shared";

export function useStream(url: string) {
  const [worldState, setWorldState] = useState<WorldState | null>(null);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const stateRef = useRef<WorldState>(createWorldState());
  const callbacksRef = useRef(createStreamCallbacks(stateRef.current));

  const flush = useCallback(() => {
    setWorldState({
      ...stateRef.current,
      sections: new Map(stateRef.current.sections),
      dirtySectionKeys: new Set(stateRef.current.dirtySectionKeys),
      blockEntities: [...stateRef.current.blockEntities],
      entities: [...stateRef.current.entities],
    });
  }, []);

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;
    setError(null);
    stateRef.current = createWorldState();
    callbacksRef.current = createStreamCallbacks(stateRef.current);
    const ws = new WebSocket(url);
    wsRef.current = ws;

    ws.onopen = () => {
      setConnected(true);
      flush();
    };
    ws.onclose = (event) => {
      setConnected(false);
      wsRef.current = null;
      // Show close reason when connection fails (e.g. "Mod connection failed" from relay)
      if (event.code !== 1000 && event.reason) {
        setError(event.reason);
      }
    };
    ws.onerror = () => {
      setError("WebSocket error (is relay running? pnpm dev:server)");
    };
    ws.onmessage = (event) => {
      const data = event.data;
      if (typeof data === "string") {
        parseText(data, callbacksRef.current);
      } else if (data instanceof ArrayBuffer) {
        parseBinary(new Uint8Array(data), callbacksRef.current);
      } else if (data instanceof Blob) {
        data.arrayBuffer().then((buf) => {
          parseBinary(new Uint8Array(buf), callbacksRef.current);
          flush();
        });
        return;
      }
      flush();
    };
  }, [url, flush]);

  const disconnect = useCallback(() => {
    wsRef.current?.close();
    wsRef.current = null;
    setConnected(false);
  }, []);

  useEffect(() => {
    return () => {
      wsRef.current?.close();
    };
  }, []);

  return { worldState, connected, error, connect, disconnect };
}
