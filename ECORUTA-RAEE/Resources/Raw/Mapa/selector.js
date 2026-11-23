(() => {
  "use strict";
  const config = JSON.parse(document.getElementById("config").textContent);
  const estado = document.getElementById("estado");
  const mapa = L.map("mapa", { maxZoom: 19, minZoom: 3, maxBounds: [[-85.05, -180], [85.05, 180]] })
    .setView(config.inicial || [18.48, -69.93], config.inicial ? 16 : 12);
  mapa.attributionControl.setPrefix(false);
  const capa = L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    noWrap: true, keepBuffer: 0,
    attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  });
  let marcador = null;
  let mapaSinTeselas = true;
  let cargandoTeselas = config.internet;

  function actualizarEstado() {
    if (mapaSinTeselas) marcador?.dragging?.disable();
    else marcador?.dragging?.enable();
    estado.textContent = cargandoTeselas && mapaSinTeselas ? "Cargando mapa…"
      : mapaSinTeselas
      ? "Mapa base no disponible. Puedes volver y usar la dirección o las coordenadas avanzadas."
      : marcador ? "Arrastra el marcador para ajustar la ubicación." : "Toca el mapa para elegir un punto.";
  }

  function colocar(latitud, longitud, centrar) {
    if (!Number.isFinite(latitud) || !Number.isFinite(longitud) ||
        Math.abs(latitud) > 90 || Math.abs(longitud) > 180)
      return false;

    const punto = [latitud, longitud];
    if (marcador) {
      marcador.setLatLng(punto);
    } else {
      const icono = L.divIcon({className:"punto-recogida", iconSize:[26,26], iconAnchor:[13,13]});
      marcador = L.marker(punto, {icon:icono, draggable:true, title:"Punto de recogida"}).addTo(mapa);
      marcador.on("dragend", () => {
        const posicion = marcador.getLatLng().wrap();
        marcador.setLatLng(posicion);
        actualizarEstado();
      });
    }
    if (centrar) mapa.setView(punto, 16);
    actualizarEstado();
    return true;
  }

  capa.on("loading", () => { cargandoTeselas = true; mapaSinTeselas = true; actualizarEstado(); });
  capa.on("load", () => { cargandoTeselas = false; actualizarEstado(); });
  capa.on("tileload", () => { mapaSinTeselas = false; actualizarEstado(); });
  if (config.internet) capa.addTo(mapa);
  else L.control.attribution({position:"bottomright",prefix:false})
    .addAttribution('© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors').addTo(mapa);

  mapa.on("click", evento => {
    if (mapaSinTeselas) return;
    const punto = evento.latlng.wrap();
    colocar(punto.lat, punto.lng, false);
  });

  if (config.inicial) colocar(...config.inicial, false);
  actualizarEstado();
  window.ecoruta = Object.freeze({
    sesion: config.sesion,
    colocar,
    leer: () => marcador ? config.sesion + "|" + marcador.getLatLng().lat + "|" + marcador.getLatLng().lng : ""
  });
})();
