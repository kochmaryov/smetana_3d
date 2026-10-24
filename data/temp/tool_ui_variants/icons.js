/* icons.js — простые векторные глифы инструментов для макетов (viewBox 24x24) */
(function () {
  var P = {
    select: "M5 3l13.2 8.3-6 .8-2.3 6.4z",
    line: "M4 20L20 4M3.2 20.8a1.2 1.2 0 102.4 0 1.2 1.2 0 10-2.4 0M18.4 3.2a1.2 1.2 0 102.4 0 1.2 1.2 0 10-2.4 0",
    rect: "M4 6h16v12H4z",
    circle: "M12 4a8 8 0 100 16 8 8 0 100-16",
    polygon: "M12 3l8 4.5v9L12 21l-8-4.5v-9z",
    arc: "M4 18Q12 3 20 18M3 18h.01M21 18h.01",
    freehand: "M4 15c2-5 4 2 6-3s4 3 6-2 3 1 4 0",
    pushpull: "M4 13h12v7H4zM10 10V3M6.5 6.5L10 3l3.5 3.5",
    move: "M12 3v18M3 12h18M12 3l-2.5 2.6M12 3l2.5 2.6M12 21l-2.5-2.6M12 21l2.5-2.6M3 12l2.6-2.5M3 12l2.6 2.5M21 12l-2.6-2.5M21 12l-2.6 2.5",
    rotate: "M20 12a8 8 0 11-2.4-5.7M20 3.5v4h-4",
    scale: "M5 5h8v8H5zM13 13l6 6M15 19h4v-4M5 19h2",
    eraser: "M7 20l-3.2-3.2 9-9L19 14l-6 6zM10 8l6 6M7 20h13",
    offset: "M4 4h16v16H4zM8 8h8v8H8z",
    soften: "M4 16l3-5 3 3 4-7 6 6M4 20h16",
    paint: "M5 10l6-6 7 6-6 6zM12 16v2.5a2 2 0 104 0V16",
    arrayLinear: "M3 6h5v5H3zM10 6h5v5h-5zM17 6h5v5h-5zM3 14h5v5H3zM10 14h5v5h-5zM17 14h5v5h-5z",
    arrayPolar: "M12 3a9 9 0 100 18 9 9 0 100-18M12 8.5a3.5 3.5 0 100 7 3.5 3.5 0 100-7M12 3v5.4M12 15.6V21M3 12h5.4M15.6 12H21",
    mirror: "M12 2v20M9.5 6.5L4 12l5.5 5.5zM14.5 6.5L20 12l-5.5 5.5z",
    followme: "M4 5h6v6H4zM10 8c6 0 4 11 10 11M20 19l-2.6-1.6M20 19l-1.2 2.8",
    revolve: "M5 3v18M8 6h4.5v6H8zM12.5 8c5 1 7 4.5 7 8.5M19.5 16.5l-2.3-1.4M19.5 16.5l-1 2.6",
    dimension: "M4 5.5v13M20 5.5v13M4 12h16M7 9l-3 3 3 3M17 9l3 3-3 3",
    scaleDim: "M5 5h6v6H5zM11 11l5 5M13 19h6M16 16v6M4 19v-3M4 19h3",
    printer: "M7 8V4h10v4M7 17H5a1 1 0 01-1-1v-6h16v6a1 1 0 01-1 1h-2M7 14h10v6H7z",
    eye: "M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7zM12 9a3 3 0 100 6 3 3 0 100-6",
    gear: "M12 9a3 3 0 100 6 3 3 0 100-6M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3M5.2 5.2l2.1 2.1M16.7 16.7l2.1 2.1M18.8 5.2l-2.1 2.1M7.3 16.7l-2.1 2.1",
    undo: "M4 12h10.5a4.5 4.5 0 110 9H10M4 12l4.2-4.2M4 12l4.2 4.2",
    redo: "M20 12H9.5a4.5 4.5 0 100 9H14M20 12l-4.2-4.2M20 12l-4.2 4.2",
    fileNew: "M6 3h8l4 4v14H6zM14 3v4h4M9 13h6M12 10v6",
    open: "M3 7h6l2 2h10v10H3zM3 7v12",
    save: "M5 3h11l3 3v15H5zM8 3v6h8V3M8 14h8v7H8z",
    search: "M10 4a6 6 0 100 12 6 6 0 100-12M14.8 14.8L20 20",
    pin: "M9.5 3h5l-1 6 3.2 3.2H7.3L10.5 9zM12 12.2V21",
    plus: "M12 5v14M5 12h14",
    chevron: "M9 6l6 6-6 6",
    grip: "M9 6h.01M9 12h.01M9 18h.01M15 6h.01M15 12h.01M15 18h.01",
    check: "M4 12l5 5L20 6",
    globe: "M12 3a9 9 0 100 18 9 9 0 100-18M3 12h18M12 3c3 3.2 3 14.8 0 18M12 3c-3 3.2-3 14.8 0 18",
    theme: "M12 7a5 5 0 100 10 5 5 0 100-10M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5L19 19M19 5l-1.5 1.5M6.5 17.5L5 19",
    book: "M4 5a2 2 0 012-2h12v18H6a2 2 0 01-2-2zM8 7.5h8M8 11.5h8",
    key: "M16 4a5 5 0 11-3.6 8.5L6.5 18.4 4 21H2v-2l1.6-1.6 2-2 3-3A5 5 0 0116 4zM16.4 7.6h.01",
    cube: "M12 2l9 5v10l-9 5-9-5V7zM12 12l9-5M12 12v10M12 12L3 7",
    layers: "M12 3l9 5-9 5-9-5zM3 13l9 5 9-5",
    terminal: "M4 5h16v14H4zM7.5 9.5l3 2.8-3 2.8M12.5 15h4.5",
    sliders: "M4 7h9M18.5 7H20M4 12h4.5M13.5 12H20M4 17h12M20 17h0M13 5v4M8.5 10v4M16 15v4",
    zoom: "M10 4a6 6 0 100 12 6 6 0 100-12M14.8 14.8L20 20M8 10h4M10 8v4",
    home: "M4 11l8-7 8 7M6.5 9.5V20h11V9.5M10 20v-6h4v6",
    dot: "M12 10a2 2 0 100 4 2 2 0 100-4"
  };

  function render() {
    var nodes = document.querySelectorAll("[data-ic]");
    for (var i = 0; i < nodes.length; i++) {
      var el = nodes[i];
      if (el.getAttribute("data-ic-done")) continue;
      var name = el.getAttribute("data-ic") || "dot";
      var d = P[name] || P.dot;
      el.innerHTML =
        '<svg class="ic' + (el.getAttribute("data-ic-fill") ? " fill" : "") +
        '" viewBox="0 0 24 24" aria-hidden="true"><path d="' + d + '"/></svg>';
      el.setAttribute("data-ic-done", "1");
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", render);
  } else {
    render();
  }
})();
