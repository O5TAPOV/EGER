import { useLayoutEffect, useState } from "react";

export function useAnchoredBox(open, anchorRef) {
  const [box, setBox] = useState(null);

  useLayoutEffect(() => {
    if (!open) return undefined;
    const update = () => {
      const node = anchorRef.current;
      if (!node) return;
      const rect = node.getBoundingClientRect();
      const margin = 8;
      const width = Math.max(rect.width, 160);
      let left = rect.left;
      if (left + width > window.innerWidth - margin) {
        left = Math.max(margin, window.innerWidth - margin - width);
      }
      const spaceBelow = window.innerHeight - rect.bottom;
      const openUp = spaceBelow < 160 && rect.top > spaceBelow;
      setBox({
        position: "fixed",
        left,
        width,
        zIndex: 60,
        top: openUp ? "auto" : rect.bottom + 4,
        bottom: openUp ? window.innerHeight - rect.top + 4 : "auto",
        maxHeight: Math.max(120, (openUp ? rect.top : spaceBelow) - 12),
        backgroundColor: "#10241c",
      });
    };
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [open, anchorRef]);

  return open ? box : null;
}
