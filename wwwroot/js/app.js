let currentView = 'table';
let currentLang = 'en-US';
let currentSeed = 12345;
let currentAvgLikes = 5.0;

let tablePage = 1;
let galleryPage = 1;

const pageSize = 15;

let galleryLoading = false;
let galleryHasMore = true;

let galleryObserver = null;

const reviewsLocalization = {
    'en-US': [
        "Absolutely mind-blowing!",
        "Nice vibe, good rhythm.",
        "A bit boring, sorry.",
        "Fire! Will replay.",
        "Too short, but sweet.",
        "AI-generated? Sounds human!",
        "Melodic masterpiece.",
        "Drums are incredible.",
        "Piano solo is touching.",
        "Instant classic."
    ],
    'ru-RU': [
        "Просто потрясающе!",
        "Хороший вайб, отличный ритм.",
        "Немного скучновато...",
        "Огонь! Буду переслушивать.",
        "Коротко, но мило.",
        "Сгенерировано ИИ? Звучит как живое!",
        "Мелодичный шедевр.",
        "Барабаны невероятны.",
        "Соло на пианино трогает душу.",
        "Мгновенная классика!"
    ]
};
const headerTranslations = {
    'en-US': {
        langLabel: 'Language',
        seedLabel: 'Seed',
        randomBtn: 'Random Seed',
        likesLabel: 'Avg Likes (0–10)',
        tableView: 'Table View',
        galleryView: 'Gallery View',
        exportBtn: 'Export ZIP',
        loader: 'Loading more songs...'
    },
    'ru-RU': {
        langLabel: 'Язык',
        seedLabel: 'Сид',
        randomBtn: 'Случайный сид',
        likesLabel: 'Средние лайки (0–10)',
        tableView: 'Таблица',
        galleryView: 'Галерея',
        exportBtn: 'Экспорт ZIP',
        loader: 'Загрузка песен...'
    }
};

function updateHeaderLanguage() {
    const t = headerTranslations[currentLang];
    if (!t) return;

    const langLabel = document.getElementById('langLabel');
    const seedLabel = document.getElementById('seedLabel');
    const likesLabel = document.getElementById('likesLabel');
    const randomBtn = document.getElementById('randomSeedBtn');
    const tableViewBtn = document.getElementById('tableViewBtn');
    const galleryViewBtn = document.getElementById('galleryViewBtn');
    const exportBtn = document.getElementById('exportBtn');
    const loader = document.getElementById('galleryLoader');

    if (langLabel) langLabel.textContent = t.langLabel;
    if (seedLabel) seedLabel.textContent = t.seedLabel;
    if (likesLabel) likesLabel.textContent = t.likesLabel;
    if (randomBtn) randomBtn.textContent = t.randomBtn;
    if (tableViewBtn) tableViewBtn.textContent = t.tableView;
    if (galleryViewBtn) galleryViewBtn.textContent = t.galleryView;
    if (exportBtn) exportBtn.textContent = t.exportBtn;
    if (loader) loader.textContent = t.loader;
}

const langSelect = document.getElementById('langSelect');
const seedInput = document.getElementById('seedInput');
const randomSeedBtn = document.getElementById('randomSeedBtn');
const likesInput = document.getElementById('likesInput');

const tableViewBtn = document.getElementById('tableViewBtn');
const galleryViewBtn = document.getElementById('galleryViewBtn');

const tableContainer = document.getElementById('tableContainer');
const galleryContainer = document.getElementById('galleryContainer');

const paginationControls = document.getElementById('paginationControls');

const exportBtn = document.getElementById('exportBtn');

const galleryLoader = document.getElementById('galleryLoader');
const gallerySentinel = document.getElementById('gallerySentinel');

function randomSeed64() {
    return Math.floor(Math.random() * Number.MAX_SAFE_INTEGER);
}

async function fetchSongs(page) {
    const url = `/api/songs?lang=${currentLang}&seed=${currentSeed}&page=${page}&pageSize=${pageSize}&avgLikes=${currentAvgLikes}`;
    const resp = await fetch(url);
    if (!resp.ok) throw new Error('Network error');
    return await resp.json();
}

function fetchReview(audioSeed) {
    let x = Number(audioSeed);
    const rng = () => {
        x = (x * 1664525 + 1013904223) % 2 ** 32;
        return x / 2 ** 32;
    };

    const reviews = reviewsLocalization[currentLang] || reviewsLocalization['en-US'];
    return reviews[Math.floor(rng() * reviews.length)];
}

function escapeHtml(str) {
    return str.replace(/[&<>]/g, m => {
        if (m === '&') return '&amp;';
        if (m === '<') return '&lt;';
        if (m === '>') return '&gt;';
    });
}

async function loadCoverAsync(imgElement, url) {
    try {
        const response = await fetch(url);
        if (response.ok) {
            const blob = await response.blob();
            imgElement.src = URL.createObjectURL(blob);
        }
    } catch (err) {
        console.error(err);
    }
}

function showLoader(text = 'Loading more songs...') {
    if (!galleryLoader) return;
    galleryLoader.textContent = text;
    galleryLoader.classList.remove('hidden');
}

function hideLoader() {
    if (!galleryLoader) return;
    galleryLoader.classList.add('hidden');
}

async function renderTable() {
    const songs = await fetchSongs(tablePage);

    let html = `
    <table>
        <thead>
            <tr>
                <th>#</th><th>Title</th><th>Artist</th>
                <th>Album</th><th>Genre</th><th>Likes</th>
            </tr>
        </thead>
        <tbody>
    `;

    songs.forEach(song => {
        html += `
        <tr data-index="${song.index}"
            data-seed="${song.coverSeed}"
            data-audioseed="${song.audioSeed}"
            data-title="${escapeHtml(song.title)}"
            data-artist="${escapeHtml(song.artist)}"
            data-album="${escapeHtml(song.album)}"
            data-genre="${escapeHtml(song.genre)}">
            <td>${song.index}</td>
            <td>${escapeHtml(song.title)}</td>
            <td>${escapeHtml(song.artist)}</td>
            <td>${escapeHtml(song.album)}</td>
            <td>${escapeHtml(song.genre)}</td>
            <td>${song.likes}</td>
        </tr>`;
    });

    html += `</tbody></table>`;
    tableContainer.innerHTML = html;

    document.querySelectorAll('#tableContainer tbody tr').forEach(row => {
        row.addEventListener('click', async () => {

            if (row.nextElementSibling?.classList.contains('expanded-row')) {
                row.nextElementSibling.remove();
                return;
            }

            document.querySelectorAll('.expanded-row').forEach(r => r.remove());

            const title = row.dataset.title;
            const artist = row.dataset.artist;
            const album = row.dataset.album;
            const genre = row.dataset.genre;
            const coverSeed = row.dataset.seed;
            const audioSeed = row.dataset.audioseed;

            const review = fetchReview(audioSeed);

            const coverUrl =
                `/api/cover?seed=${coverSeed}&title=${encodeURIComponent(title)}&artist=${encodeURIComponent(artist)}&genre=${encodeURIComponent(genre)}`;

            const audioUrl = `/api/audio?seed=${audioSeed}`;

            const expanded = document.createElement('tr');
            expanded.className = 'expanded-row';

            expanded.innerHTML = `
            <td colspan="6">
                <div class="expanded-content">
                    <img class="cover-preview"
                         src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='100'%3E%3Crect width='100' height='100' fill='%23667eea'/%3E%3Ctext x='50' y='50' text-anchor='middle' dy='.3em' fill='white'%3E%3C/text%3E%3C/svg%3E">
                    <div>
                        <strong>${escapeHtml(title)}</strong> by ${escapeHtml(artist)}<br>
                        Album: ${escapeHtml(album)}<br>
                        <audio controls src="${audioUrl}"></audio>
                        <div class="review">${escapeHtml(review)}</div>
                    </div>
                </div>
            </td>`;

            row.insertAdjacentElement('afterend', expanded);

            loadCoverAsync(expanded.querySelector('img'), coverUrl);
        });
    });

    renderPagination();
}

function renderPagination() {
    paginationControls.innerHTML = `
        <button ${tablePage === 1 ? 'disabled' : ''} id="prevPage">Prev</button>
        <span>Page ${tablePage}</span>
        <button id="nextPage">Next</button>
    `;

    document.getElementById('prevPage')?.addEventListener('click', () => {
        tablePage--;
        renderTable();
    });

    document.getElementById('nextPage')?.addEventListener('click', () => {
        tablePage++;
        renderTable();
    });
}

async function loadMoreGallery() {
    if (galleryLoading || !galleryHasMore) return;

    galleryLoading = true;
    showLoader();

    try {
        const songs = await fetchSongs(galleryPage);

        if (!songs.length) {
            galleryHasMore = false;
            showLoader('No more songs...');
            return;
        }

        for (const song of songs) {
            const coverUrl =
                `/api/cover?seed=${song.coverSeed}&title=${encodeURIComponent(song.title)}&artist=${encodeURIComponent(song.artist)}&genre=${encodeURIComponent(song.genre)}`;

            const audioUrl = `/api/audio?seed=${song.audioSeed}`;

            const card = document.createElement('div');
            card.className = 'card';

            card.innerHTML = `
                <img src="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='400' height='180'%3E%3Crect width='400' height='180' fill='%23667eea'/%3E%3Ctext x='200' y='90' text-anchor='middle' fill='white'%3ELoading...%3C/text%3E%3C/svg%3E">
                <div class="card-body">
                    <strong>${song.index}. ${escapeHtml(song.title)}</strong><br>
                    ${escapeHtml(song.artist)}<br>
                    Album: ${escapeHtml(song.album)}<br>
                    Genre: ${escapeHtml(song.genre)}<br>
                    Likes: ${song.likes}<br>
                    <audio controls src="${audioUrl}"></audio>
                </div>
            `;

            galleryContainer.appendChild(card);
            loadCoverAsync(card.querySelector('img'), coverUrl);
        }

        galleryPage++;

    } finally {
        galleryLoading = false;

        if (galleryHasMore) hideLoader();
    }
}

function resetGallery() {
    galleryContainer.innerHTML = '';
    galleryPage = 1;
    galleryHasMore = true;
    galleryLoading = false;

    hideLoader();
    loadMoreGallery();
}

function initInfiniteScroll() {
    if (galleryObserver) galleryObserver.disconnect();

    galleryObserver = new IntersectionObserver(entries => {
        const entry = entries[0];

        if (
            entry.isIntersecting &&
            currentView === 'gallery' &&
            !galleryLoading &&
            galleryHasMore
        ) {
            loadMoreGallery();
        }
    }, {
        root: null,
        rootMargin: '300px',
        threshold: 0
    });

    galleryObserver.observe(gallerySentinel);
}

function setView(view) {
    currentView = view;

    if (view === 'table') {
        tableContainer.classList.remove('hidden');
        galleryContainer.classList.add('hidden');
        paginationControls.classList.remove('hidden');
        renderTable();
    } else {
        tableContainer.classList.add('hidden');
        galleryContainer.classList.remove('hidden');
        paginationControls.classList.add('hidden');

        resetGallery();
        initInfiniteScroll();
    }

    tableViewBtn.classList.toggle('active', view === 'table');
    galleryViewBtn.classList.toggle('active', view === 'gallery');
}

function onParamsChange() {
    if (currentView === 'table') {
        tablePage = 1;
        renderTable();
    } else {
        resetGallery();
        window.scrollTo(0, 0);
    }
}

langSelect.onchange = () => {
    currentLang = langSelect.value;
    updateHeaderLanguage();
    onParamsChange();
};

seedInput.oninput = () => {
    currentSeed = parseInt(seedInput.value, 10) || 0;
    onParamsChange();
};

randomSeedBtn.onclick = () => {
    currentSeed = randomSeed64();
    seedInput.value = currentSeed;
    onParamsChange();
};

likesInput.oninput = () => {
    currentAvgLikes = Math.min(10, Math.max(0, parseFloat(likesInput.value) || 0));
    onParamsChange();
};

tableViewBtn.onclick = () => setView('table');
galleryViewBtn.onclick = () => setView('gallery');

exportBtn.onclick = async () => {
    const songs = await fetchSongs(galleryPage);

    const payload = songs.map(s => ({
        seed: currentSeed,
        index: s.index
    }));

    const resp = await fetch('/api/export', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            lang: currentLang,
            avgLikes: currentAvgLikes,
            songs: payload
        })
    });

    if (!resp.ok) return alert('Export failed');

    const blob = await resp.blob();
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = 'songs.zip';
    a.click();

    URL.revokeObjectURL(url);
};

document.addEventListener('DOMContentLoaded', () => {
    setView('table');
    updateHeaderLanguage();
});