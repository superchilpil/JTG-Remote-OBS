// The dock intentionally remains display-only until the companion's
// authenticated event channel and YouTube authorization are implemented.
const status = document.getElementById("companion-status");
fetch("http://127.0.0.1:47831/api/v1/health")
  .then(response => response.ok ? response.json() : Promise.reject())
  .then(data => {
    status.textContent = data.status.toUpperCase();
    document.getElementById("obs-state").textContent = data.obs;
    document.getElementById("youtube-state").textContent = data.youtube;
  })
  .catch(() => { status.textContent = "OFFLINE"; });
