document.addEventListener('DOMContentLoaded', () => {
    const fallbackProductImage = 'https://images.unsplash.com/photo-1581091870627-3a5c2e2d6f2f?w=400';
    function normalizeText(value) {
        return `${value || ''}`
            .normalize('NFD')
            .replace(/\p{Diacritic}/gu, '')
            .toLowerCase()
            .trim();
    }

    function mapCatalogProduct(product) {
        const name = product.name || 'Producto sin nombre';
        const category = product.category || 'Sin categoría';
        const description = product.description || 'Sin descripción disponible.';

        return {
            id: product.id,
            name,
            category,
            price: Number(product.price || 0),
            originalPrice: null,
            image: product.image || product.image2 || product.image3 || fallbackProductImage,
            image2: product.image2 || null,
            image3: product.image3 || null,
            description,
            rating: Number(product.rating || 4.5),
            stock: Number(product.stock || 0),
            minStock: Number(product.minStock || 0),
            searchIndex: normalizeText(`${name} ${category} ${description}`)
        };
    }

    let products = Array.isArray(window.portadaData?.products)
        ? window.portadaData.products.map(mapCatalogProduct)
        : [];
    let hasLoadedCatalog = products.length > 0;
    let currentFilter = 'Todo';
    let currentSearchTerm = '';
    let currentSortOption = 'relevance';
    let currentMinPrice = '';
    let currentMaxPrice = '';
    const productsPerPage = 12;
    let currentPage = 1;

    const grid = document.getElementById('productsGrid');
    const catalogPagination = document.getElementById('catalogPagination');
    const modal = document.getElementById('modal');
    const modalContent = document.getElementById('modalContent');
    const closeModal = document.getElementById('closeModal');
    const notificationToggle = document.getElementById('notificationToggle');
    const notificationBadge = document.getElementById('notificationBadge');
    const notificationsMenu = document.getElementById('notificationsMenu');
    const notificationsMenuBody = document.getElementById('notificationsMenuBody');
    const notificationsMarkRead = document.getElementById('notificationsMarkRead');
    const userProfileToggle = document.getElementById('userProfileToggle');
    const userProfileMenu = document.getElementById('userProfileMenu');
    const goToMyProfile = document.getElementById('goToMyProfile');
    const goToStoreHome = document.getElementById('goToStoreHome');
    const backToStoreFromProfile = document.getElementById('backToStoreFromProfile');
    const storefrontSection = document.getElementById('storefrontSection');
    const clientProfileSection = document.getElementById('clientProfileSection');
    const profileImageFile = document.getElementById('profileImageFile');
    const profileImageUrl = document.getElementById('profileImageUrl');
    const profileImagePreview = document.getElementById('profileImagePreview');
    const profileAvatarLabel = document.getElementById('profileAvatarLabel');
    const userHeaderAvatar = document.getElementById('userHeaderAvatar');
    const profileForm = document.querySelector('.profile-form');
    const profileSaveButton = profileForm?.querySelector('.profile-save-btn');
    const profileFirstName = profileForm?.querySelector('input[name="FirstName"]');
    const profileLastName = profileForm?.querySelector('input[name="LastName"]');
    const profilePhone = profileForm?.querySelector('input[name="Phone"]');
    const openCartPanelButton = document.getElementById('openCartPanel');
    const closeCartPanelButton = document.getElementById('closeCartPanel');
    const openOrderHistoryPanelButton = document.getElementById('openOrderHistoryPanel');
    const closeOrderHistoryPanelButton = document.getElementById('closeOrderHistoryPanel');
    const openSupportPanelButton = document.getElementById('openSupportPanel');
    const closeSupportPanelButton = document.getElementById('closeSupportPanel');
    const cartPanel = document.getElementById('cartPanel');
    const cartOverlay = document.getElementById('cartOverlay');
    const orderHistoryPanel = document.getElementById('orderHistoryPanel');
    const orderHistoryOverlay = document.getElementById('orderHistoryOverlay');
    const supportPanel = document.getElementById('supportPanel');
    const supportOverlay = document.getElementById('supportOverlay');
    const cartPanelBody = document.getElementById('cartPanelBody');
    const cartBadgeCount = document.getElementById('cartBadgeCount');
    const checkoutCartButton = document.getElementById('checkoutCartButton');
    const cartTotal = document.getElementById('cartTotal');
    const purchaseHistoryContainer = document.getElementById('purchaseHistoryContainer');
    const contactSupportForm = document.getElementById('contactSupportForm');
    const contactSupportMessage = document.getElementById('contactSupportMessage');
    const contactSupportSubmitButton = document.getElementById('contactSupportSubmitButton');
    const clientSupportInbox = document.getElementById('clientSupportInbox');
    const clientSupportInboxLoadUrl = `${clientSupportInbox?.dataset.loadUrl || ''}`;
    const clientSupportInboxReplyUrl = `${clientSupportInbox?.dataset.replyUrl || ''}`;
    const paymentModalOverlay = document.getElementById('paymentModalOverlay');
    const closePaymentModalButton = document.getElementById('closePaymentModal');
    const cancelPaymentButton = document.getElementById('cancelPaymentButton');
    const confirmPaymentButton = document.getElementById('confirmPaymentButton');
    const paymentValidationMessage = document.getElementById('paymentValidationMessage');
    const paymentMethodButtons = Array.from(document.querySelectorAll('[data-payment-method]'));
    const paymentMethodPanels = Array.from(document.querySelectorAll('[data-payment-panel]'));
    const paymentCardHolder = document.getElementById('paymentCardHolder');
    const paymentCardNumber = document.getElementById('paymentCardNumber');
    const paymentCardMonth = document.getElementById('paymentCardMonth');
    const paymentCardYear = document.getElementById('paymentCardYear');
    const paymentCardCvv = document.getElementById('paymentCardCvv');
    const paypalButtonsContainer = document.getElementById('paypalButtonsContainer');
    const paymentCashCustomerName = document.getElementById('paymentCashCustomerName');
    const catalogSearchInput = document.getElementById('catalogSearchInput');
    const catalogSearchButton = document.getElementById('catalogSearchButton');
    const catalogSortSelect = document.getElementById('catalogSortSelect');
    const catalogMinPriceInput = document.getElementById('catalogMinPrice');
    const catalogMaxPriceInput = document.getElementById('catalogMaxPrice');
    const catalogApplyPriceFilterButton = document.getElementById('catalogApplyPriceFilter');
    const catalogClearPriceFilterButton = document.getElementById('catalogClearPriceFilter');
    const isAuthenticated = window.portadaData?.isAuthenticated === true
        || !!document.getElementById('userProfileToggle')
        || !!(`${window.portadaData?.userEmail || ''}`.trim());
    const paypalClientId = `${window.portadaData?.paypalClientId || ''}`.trim();
    const paypalEnabled = window.portadaData?.paypalEnabled === true || !!paypalClientId;
    const resolvedPayPalClientId = paypalClientId || 'sb';
    const loginUrl = typeof window.portadaData?.loginUrl === 'string' && window.portadaData.loginUrl.trim()
        ? window.portadaData.loginUrl
        : '/Home/Login';
    const legacyCartStorageKey = 'nexo_cart';
    const guestCartStorageKey = 'nexo_cart_guest';
    const userEmail = normalizeText(window.portadaData?.userEmail || '');
    const cartStorageKey = isAuthenticated && userEmail
        ? `nexo_cart_${userEmail}`
        : guestCartStorageKey;
    const notificationsStorageKey = isAuthenticated && userEmail
        ? `nexo_notifications_${userEmail}`
        : 'nexo_notifications_guest';
    const supportResponsesStorageKey = isAuthenticated && userEmail
        ? `nexo_support_responses_${userEmail}`
        : 'nexo_support_responses_guest';
    let cartItems = [];
    let notifications = [];
    let supportResponseTracker = {};
    let cartToastTimer;
    let productSearchDebounceTimer;
    let selectedPaymentMethod = 'tarjeta';
    let paypalOrderId = '';
    let purchaseHistory = [];
    let payPalSdkPromise;

    function formatCurrency(value) {
        return new Intl.NumberFormat('es-PE', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(Number(value || 0));
    }

    function formatDateTime(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '-';
        }

        return new Intl.DateTimeFormat('es-PE', {
            dateStyle: 'medium',
            timeStyle: 'short'
        }).format(date);
    }

    function escapeHtml(value) {
        return `${value || ''}`
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function renderPurchaseHistory() {
        if (!purchaseHistoryContainer) {
            return;
        }

        if (!purchaseHistory.length) {
            purchaseHistoryContainer.innerHTML = '<p class="client-tools-empty">No hay compras registradas todavía.</p>';
            return;
        }

        purchaseHistoryContainer.innerHTML = `
            <div class="purchase-history-list">
                ${purchaseHistory.map(purchase => {
                    const receiptId = `${purchase.id || ''}`.trim();
                    const downloadUrl = `/Home/DownloadPurchaseReceiptPdf?receiptId=${encodeURIComponent(receiptId)}`;
                    return `
                        <article class="purchase-history-card">
                            <div class="purchase-history-card-head">
                                <strong>${escapeHtml(purchase.receiptNumber)}</strong>
                                <span class="purchase-status-badge ${purchase.status === 'pagado' ? 'paid' : 'pending'}">${escapeHtml(purchase.status || 'pendiente')}</span>
                            </div>
                            <div class="purchase-history-card-meta">
                                <span><i class="far fa-clock"></i> ${formatDateTime(purchase.createdAtUtc)}</span>
                                <span><i class="fas fa-credit-card"></i> ${escapeHtml(purchase.paymentMethod || '-')}</span>
                            </div>
                            <div class="purchase-history-card-total">Total: ${formatCurrency(purchase.total)}</div>
                            <a class="purchase-history-download" href="${downloadUrl}" target="_blank" rel="noopener noreferrer">
                                <i class="fas fa-file-pdf"></i> Descargar comprobante PDF
                            </a>
                        </article>
                    `;
                }).join('')}
            </div>
        `;
    }

    function upsertReceipt(receipt) {
        if (!receipt || !receipt.id) {
            return;
        }

        purchaseHistory = purchaseHistory.filter(item => `${item.id}` !== `${receipt.id}`);
        purchaseHistory.unshift(receipt);
        purchaseHistory.sort((a, b) => new Date(b.createdAtUtc) - new Date(a.createdAtUtc));
        renderPurchaseHistory();
    }

    async function loadPurchaseHistory() {
        if (!isAuthenticated) {
            return;
        }

        try {
            const response = await fetch('/Home/GetPurchaseHistory', {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const payload = await response.json();
            if (!response.ok || !payload?.succeeded || !Array.isArray(payload.purchases)) {
                return;
            }

            purchaseHistory = payload.purchases;
            renderPurchaseHistory();
        } catch {
            // No se bloquea la UI si falla la carga del historial.
        }
    }

    function openOrderHistoryPanel() {
        orderHistoryPanel?.classList.add('open');
        orderHistoryPanel?.setAttribute('aria-hidden', 'false');
        if (orderHistoryOverlay) {
            orderHistoryOverlay.hidden = false;
        }
    }

    function closeOrderHistoryPanel() {
        orderHistoryPanel?.classList.remove('open');
        orderHistoryPanel?.setAttribute('aria-hidden', 'true');
        if (orderHistoryOverlay) {
            orderHistoryOverlay.hidden = true;
        }
    }

    function openSupportPanel() {
        supportPanel?.classList.add('open');
        supportPanel?.setAttribute('aria-hidden', 'false');
        if (supportOverlay) {
            supportOverlay.hidden = false;
        }

        loadClientSupportInbox();
    }

    function closeSupportPanel() {
        supportPanel?.classList.remove('open');
        supportPanel?.setAttribute('aria-hidden', 'true');
        if (supportOverlay) {
            supportOverlay.hidden = true;
        }
    }

    function showCartToast(message, isError = false) {
        let toast = document.getElementById('cartToast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'cartToast';
            toast.className = 'cart-toast';
            document.body.appendChild(toast);
        }

        toast.textContent = message;
        toast.classList.toggle('success', !isError);
        toast.classList.toggle('error', isError);
        toast.hidden = false;

        window.clearTimeout(cartToastTimer);
        cartToastTimer = window.setTimeout(() => {
            toast.hidden = true;
        }, 2200);
    }

    function loadNotifications() {
        try {
            const raw = window.localStorage.getItem(notificationsStorageKey);
            const parsed = raw ? JSON.parse(raw) : [];
            notifications = Array.isArray(parsed) ? parsed : [];
        } catch {
            notifications = [];
        }
    }

    function persistNotifications() {
        try {
            if (!notifications.length) {
                window.localStorage.removeItem(notificationsStorageKey);
                return;
            }

            window.localStorage.setItem(notificationsStorageKey, JSON.stringify(notifications.slice(0, 60)));
        } catch {
            // Sin bloqueo si localStorage falla.
        }
    }

    function loadSupportResponseTracker() {
        try {
            const raw = window.localStorage.getItem(supportResponsesStorageKey);
            const parsed = raw ? JSON.parse(raw) : {};
            supportResponseTracker = parsed && typeof parsed === 'object' ? parsed : {};
        } catch {
            supportResponseTracker = {};
        }
    }

    function persistSupportResponseTracker() {
        try {
            window.localStorage.setItem(supportResponsesStorageKey, JSON.stringify(supportResponseTracker));
        } catch {
            // Sin bloqueo si localStorage falla.
        }
    }

    function renderNotifications() {
        if (!notificationsMenuBody || !notificationBadge) {
            return;
        }

        const unreadCount = notifications.filter(item => item && item.read !== true).length;
        notificationBadge.textContent = `${unreadCount}`;
        notificationBadge.style.display = unreadCount > 0 ? 'flex' : 'none';

        if (!notifications.length) {
            notificationsMenuBody.innerHTML = '<p class="notifications-empty">No hay notificaciones.</p>';
            return;
        }

        notificationsMenuBody.innerHTML = notifications
            .slice(0, 30)
            .map(item => `
                <article class="notification-item ${item.read === true ? '' : 'unread'}">
                    <p>${escapeHtml(item.message || '')}</p>
                    <small>${formatDateTime(item.createdAtUtc)}</small>
                </article>
            `)
            .join('');
    }

    function addNotification(message) {
        const normalized = `${message || ''}`.trim();
        if (!normalized) {
            return;
        }

        notifications.unshift({
            id: `${Date.now()}_${Math.random().toString(36).slice(2, 10)}`,
            message: normalized,
            createdAtUtc: new Date().toISOString(),
            read: false
        });

        notifications = notifications.slice(0, 60);
        persistNotifications();
        renderNotifications();
    }

    function markAllNotificationsAsRead() {
        if (!notifications.length) {
            return;
        }

        notifications = notifications.map(item => ({ ...item, read: true }));
        persistNotifications();
        renderNotifications();
    }

    function openNotificationsMenu() {
        notificationsMenu?.classList.add('open');
        notificationToggle?.setAttribute('aria-expanded', 'true');
    }

    function closeNotificationsMenu() {
        notificationsMenu?.classList.remove('open');
        notificationToggle?.setAttribute('aria-expanded', 'false');
    }

    function processSupportResponseNotifications(conversations) {
        if (!Array.isArray(conversations) || !conversations.length) {
            return;
        }

        let trackerUpdated = false;
        conversations.forEach(conversation => {
            const conversationId = `${conversation?.conversationId || ''}`.trim();
            if (!conversationId) {
                return;
            }

            const messages = Array.isArray(conversation.messages) ? conversation.messages : [];
            const adminMessages = messages
                .filter(message => !message?.isSystemEvent && `${message?.senderRole || ''}`.toLowerCase() === 'administrador')
                .sort((left, right) => new Date(left.createdAtUtc) - new Date(right.createdAtUtc));

            const latestAdminMessage = adminMessages.at(-1);
            if (!latestAdminMessage?.createdAtUtc) {
                return;
            }

            const latestTimestamp = `${latestAdminMessage.createdAtUtc}`;
            const trackedTimestamp = `${supportResponseTracker[conversationId] || ''}`;

            if (!trackedTimestamp) {
                supportResponseTracker[conversationId] = latestTimestamp;
                trackerUpdated = true;
                return;
            }

            if (new Date(latestTimestamp) > new Date(trackedTimestamp)) {
                addNotification(`Se recibió una respuesta del reporte "${conversation.subject || 'Sin asunto'}".`);
                supportResponseTracker[conversationId] = latestTimestamp;
                trackerUpdated = true;
            }
        });

        if (trackerUpdated) {
            persistSupportResponseTracker();
        }
    }

    function renderCatalogPagination(totalItems) {
        if (!catalogPagination) {
            return;
        }

        const totalPages = Math.max(1, Math.ceil(totalItems / productsPerPage));
        if (totalPages <= 1) {
            catalogPagination.innerHTML = '';
            catalogPagination.hidden = true;
            return;
        }

        catalogPagination.hidden = false;
        catalogPagination.innerHTML = '';

        const prevButton = document.createElement('button');
        prevButton.type = 'button';
        prevButton.className = 'catalog-page-btn nav';
        prevButton.textContent = 'Anterior';
        prevButton.disabled = currentPage <= 1;
        prevButton.addEventListener('click', () => {
            if (currentPage <= 1) {
                return;
            }

            currentPage -= 1;
            renderProducts(currentFilter, currentSearchTerm);
        });
        catalogPagination.appendChild(prevButton);

        for (let page = 1; page <= totalPages; page += 1) {
            const pageButton = document.createElement('button');
            pageButton.type = 'button';
            pageButton.className = `catalog-page-btn ${page === currentPage ? 'active' : ''}`;
            pageButton.textContent = `${page}`;
            pageButton.addEventListener('click', () => {
                if (currentPage === page) {
                    return;
                }

                currentPage = page;
                renderProducts(currentFilter, currentSearchTerm);
            });
            catalogPagination.appendChild(pageButton);
        }

        const nextButton = document.createElement('button');
        nextButton.type = 'button';
        nextButton.className = 'catalog-page-btn nav';
        nextButton.textContent = 'Siguiente';
        nextButton.disabled = currentPage >= totalPages;
        nextButton.addEventListener('click', () => {
            if (currentPage >= totalPages) {
                return;
            }

            currentPage += 1;
            renderProducts(currentFilter, currentSearchTerm);
        });
        catalogPagination.appendChild(nextButton);
    }

    function clampCatalogQuantityInput(input) {
        if (!input) {
            return;
        }

        const productId = input.dataset.qtyInput;
        const product = findProduct(productId);
        const maxStock = Math.max(1, Number(product?.stock || 1));
        const minValue = 1;
        const rawValue = Number(input.value || minValue);
        const boundedValue = Number.isFinite(rawValue)
            ? Math.min(maxStock, Math.max(minValue, Math.floor(rawValue)))
            : minValue;

        if (boundedValue !== rawValue) {
            input.value = `${boundedValue}`;
        }
    }

    function parsePriceFilterValue(value) {
        const normalized = `${value ?? ''}`.trim();
        if (!normalized.length) {
            return null;
        }

        const parsed = Number(normalized);
        if (!Number.isFinite(parsed) || parsed < 0) {
            return null;
        }

        return parsed;
    }

    function getFilteredProducts(filter = 'Todo', searchTerm = '') {
        const normalizedSearch = normalizeText(searchTerm);
        const minPrice = parsePriceFilterValue(currentMinPrice);
        const maxPrice = parsePriceFilterValue(currentMaxPrice);

        let filtered = filter === 'Todo'
            ? [...products]
            : products.filter(product => product.category === filter);

        if (normalizedSearch) {
            filtered = filtered.filter(product => product.searchIndex.includes(normalizedSearch));
        }

        if (minPrice !== null) {
            filtered = filtered.filter(product => Number(product.price || 0) >= minPrice);
        }

        if (maxPrice !== null) {
            filtered = filtered.filter(product => Number(product.price || 0) <= maxPrice);
        }

        switch (currentSortOption) {
            case 'name_asc':
                filtered.sort((left, right) => `${left.name || ''}`.localeCompare(`${right.name || ''}`, 'es', { sensitivity: 'base' }));
                break;
            case 'name_desc':
                filtered.sort((left, right) => `${right.name || ''}`.localeCompare(`${left.name || ''}`, 'es', { sensitivity: 'base' }));
                break;
            case 'price_asc':
                filtered.sort((left, right) => Number(left.price || 0) - Number(right.price || 0));
                break;
            case 'price_desc':
                filtered.sort((left, right) => Number(right.price || 0) - Number(left.price || 0));
                break;
            default:
                break;
        }

        return filtered;
    }

    function renderProducts(filter = 'Todo', searchTerm = '') {
        currentFilter = filter;
        currentSearchTerm = normalizeText(searchTerm);
        grid.innerHTML = '';

        if (!hasLoadedCatalog) {
            grid.innerHTML = '<div class="error-message">Cargando catálogo de productos...</div>';
            return;
        }

        const filtered = getFilteredProducts(filter, searchTerm);

        if (!filtered.length) {
            if (catalogPagination) {
                catalogPagination.innerHTML = '';
                catalogPagination.hidden = true;
            }
            grid.innerHTML = currentSearchTerm
                ? '<div class="error-message">No se encontraron productos relacionados con tu búsqueda.</div>'
                : '<div class="error-message">No hay productos disponibles con los filtros seleccionados.</div>';
            return;
        }

        const totalPages = Math.max(1, Math.ceil(filtered.length / productsPerPage));
        currentPage = Math.min(Math.max(currentPage, 1), totalPages);
        const startIndex = (currentPage - 1) * productsPerPage;
        const paginatedProducts = filtered.slice(startIndex, startIndex + productsPerPage);

        paginatedProducts.forEach(p => {
            const card = document.createElement('div');
            card.className = 'product-card';
            card.dataset.id = p.id;
            const quantityInputId = `productQty_${p.id}`;
            card.innerHTML = `
                <div class="product-image">
                    <img src="${p.image}" alt="${p.name}">
                    ${p.originalPrice ? `<span class="product-badge">-${Math.round((1 - p.price / p.originalPrice) * 100)}%</span>` : ''}
                    ${p.stock <= 0 ? '<span class="product-badge sold-out">AGOTADO</span>' : ''}
                </div>
                <div class="product-info">
                    <h3>${p.name}</h3>
                    <div class="product-category">${p.category}</div>
                    <p class="product-description">${p.description}</p>
                    <div class="product-price">$${p.price}</div>
                    <div class="rating"><i class="fas fa-star"></i> ${p.rating.toFixed(1)}</div>
                </div>
                <div class="product-footer">
                    <div class="product-qty-box">
                        <label for="${quantityInputId}">Cant.</label>
                        <input id="${quantityInputId}" type="number" min="1" max="${Math.max(p.stock, 1)}" step="1" value="1" data-qty-input="${p.id}" ${p.stock <= 0 ? 'disabled' : ''}>
                    </div>
                    <button class="btn-add" data-add="${p.id}" ${p.stock <= 0 ? 'disabled' : ''}><i class="fas fa-cart-plus"></i> ${p.stock <= 0 ? 'Sin stock' : 'Agregar'}</button>
                </div>
            `;
            card.querySelector('img')?.addEventListener('error', event => {
                event.target.src = fallbackProductImage;
            });
            card.addEventListener('click', event => {
                if (event.target.closest('[data-add], [data-qty-input], .product-qty-box label')) {
                    return;
                }

                openModal(p);
            });
            grid.appendChild(card);
        });

        renderCatalogPagination(filtered.length);
    }

    function openModal(p) {
        const images = [p.image, p.image2, p.image3].filter(image => typeof image === 'string' && image.trim().length > 0);
        const productImages = images.length ? images : [fallbackProductImage];
        const isLowStock = p.stock <= p.minStock;
        const modalQtyId = `modalQty_${p.id}`;

        modalContent.innerHTML = `
            <div class="modal-image">
                <div class="modal-image-viewer">
                    <img src="${productImages[0]}" alt="${p.name}" class="modal-main-image">
                    ${productImages.length > 1 ? `
                    <button type="button" class="modal-image-nav prev" data-image-nav="prev" aria-label="Imagen anterior"><i class="fas fa-chevron-left"></i></button>
                    <button type="button" class="modal-image-nav next" data-image-nav="next" aria-label="Imagen siguiente"><i class="fas fa-chevron-right"></i></button>
                    ` : ''}
                </div>
                ${productImages.length > 1 ? `
                    <div class="modal-thumbnails">
                        ${productImages.map((image, index) => `
                            <button type="button" class="modal-thumb ${index === 0 ? 'active' : ''}" data-image-index="${index}">
                                <img src="${image}" alt="${p.name} ${index + 1}">
                            </button>
                        `).join('')}
                    </div>
                ` : ''}
            </div>
            <div class="modal-details">
                <h2>${p.name}</h2>
                <div class="modal-price">$${p.price} ${p.originalPrice ? `<span style="font-size: 16px; color: #94a3b8; text-decoration: line-through;">$${p.originalPrice}</span>` : ''}</div>
                <p class="modal-description">${p.description}</p>
                <div class="modal-meta">
                    <span class="meta-item ${isLowStock ? 'low-stock' : ''}"><i class="fas fa-box"></i> Stock: ${p.stock}</span>
                    <span class="meta-item"><i class="fas fa-star" style="color: #FFB74D;"></i> ${p.rating}</span>
                </div>
                <div class="modal-qty-box">
                    <label for="${modalQtyId}">Cantidad</label>
                    <input id="${modalQtyId}" type="number" min="1" max="${Math.max(p.stock, 1)}" step="1" value="1" data-qty-input="${p.id}" ${p.stock <= 0 ? 'disabled' : ''}>
                </div>
                <button class="btn-modal-add" data-add="${p.id}" ${p.stock <= 0 ? 'disabled' : ''}>${p.stock <= 0 ? 'Sin stock' : 'Agregar al carrito'}</button>
            </div>
        `;

        const mainImage = modalContent.querySelector('.modal-main-image');
        const thumbnailButtons = Array.from(modalContent.querySelectorAll('.modal-thumb'));
        let currentImageIndex = 0;

        function renderCurrentImage(index) {
            currentImageIndex = (index + productImages.length) % productImages.length;

            if (mainImage) {
                mainImage.src = productImages[currentImageIndex];
            }

            thumbnailButtons.forEach((button, buttonIndex) => {
                button.classList.toggle('active', buttonIndex === currentImageIndex);
            });
        }

        if (mainImage) {
            mainImage.addEventListener('error', () => {
                mainImage.src = fallbackProductImage;
            });
        }

        thumbnailButtons.forEach(button => {
            button.addEventListener('click', () => {
                const index = Number(button.dataset.imageIndex || 0);
                renderCurrentImage(index);
            });
        });

        modalContent.querySelector('[data-image-nav="prev"]')?.addEventListener('click', () => {
            renderCurrentImage(currentImageIndex - 1);
        });

        modalContent.querySelector('[data-image-nav="next"]')?.addEventListener('click', () => {
            renderCurrentImage(currentImageIndex + 1);
        });

        modal.style.display = 'flex';
        document.body.classList.add('modal-open');
    }

    function closeProductModal() {
        if (!modal) {
            return;
        }

        modal.style.display = 'none';
        document.body.classList.remove('modal-open');
    }

    function findProduct(productId) {
        const normalized = `${productId}`;
        return products.find(product => `${product.id}` === normalized);
    }

    function loadCart() {
        try {
            const preferredRaw = window.localStorage.getItem(cartStorageKey);
            const fallbackRaw = window.localStorage.getItem(legacyCartStorageKey);
            const guestRaw = window.localStorage.getItem(guestCartStorageKey);

            const source = preferredRaw
                ?? (isAuthenticated ? (guestRaw ?? fallbackRaw) : fallbackRaw);

            const parsed = source ? JSON.parse(source) : [];
            cartItems = Array.isArray(parsed) ? parsed : [];

            if (isAuthenticated && cartItems.length) {
                window.localStorage.setItem(cartStorageKey, JSON.stringify(cartItems));
                window.localStorage.removeItem(guestCartStorageKey);
                window.localStorage.removeItem(legacyCartStorageKey);
            }
        } catch {
            cartItems = [];
        }
    }

    function persistCart() {
        if (!cartItems.length) {
            window.localStorage.removeItem(cartStorageKey);
            return;
        }

        window.localStorage.setItem(cartStorageKey, JSON.stringify(cartItems));
    }

    function updateCartBadge() {
        const totalQuantity = cartItems.reduce((sum, item) => sum + Number(item.quantity || 0), 0);
        if (cartBadgeCount) {
            cartBadgeCount.textContent = `${totalQuantity}`;
            cartBadgeCount.style.display = totalQuantity > 0 ? 'flex' : 'none';
        }
    }

    function renderCartPanel() {
        if (!cartPanelBody || !cartTotal) {
            return;
        }

        cartItems = cartItems.filter(item => {
            const product = findProduct(item.productId);
            return !!product && Number(product.stock || 0) > 0;
        });

        if (!cartItems.length) {
            cartPanelBody.innerHTML = '<div class="error-message" style="margin:0;">No hay productos en el carrito.</div>';
            cartTotal.textContent = 'Total: $0';
            persistCart();
            updateCartBadge();
            return;
        }

        let total = 0;
        cartPanelBody.innerHTML = cartItems.map(item => {
            const product = findProduct(item.productId);
            if (!product) {
                return '';
            }

            const safeQuantity = Math.max(1, Math.min(Number(item.quantity || 1), Number(product.stock || 1)));
            item.quantity = safeQuantity;
            total += product.price * safeQuantity;

            return `
                <div class="cart-item" data-cart-item-id="${item.productId}">
                    <div class="cart-item-title">${product.name}</div>
                    <div class="cart-item-row">
                        <span>$${product.price}</span>
                        <input class="cart-qty-input" type="number" min="1" max="${Math.max(product.stock, 1)}" value="${safeQuantity}" data-cart-qty="${item.productId}">
                        <button type="button" class="cart-remove-btn" data-cart-remove="${item.productId}">Eliminar</button>
                    </div>
                </div>`;
        }).join('');

        cartTotal.textContent = `Total: $${total.toFixed(2)}`;
        persistCart();
        updateCartBadge();
    }

    function openCartPanel() {
        cartPanel?.classList.add('open');
        cartPanel?.setAttribute('aria-hidden', 'false');
        if (cartOverlay) {
            cartOverlay.hidden = false;
        }
    }

    function closeCartPanel() {
        cartPanel?.classList.remove('open');
        cartPanel?.setAttribute('aria-hidden', 'true');
        if (cartOverlay) {
            cartOverlay.hidden = true;
        }
    }

    function setPaymentValidationMessage(message = '') {
        if (!paymentValidationMessage) {
            return;
        }

        const normalized = `${message || ''}`.trim();
        paymentValidationMessage.textContent = normalized;
        paymentValidationMessage.hidden = !normalized;
    }

    function setPaymentMethod(method) {
        selectedPaymentMethod = method;
        paymentMethodButtons.forEach(button => {
            button.classList.toggle('active', button.dataset.paymentMethod === method);
        });
        paymentMethodPanels.forEach(panel => {
            panel.classList.toggle('active', panel.dataset.paymentPanel === method);
        });

        if (confirmPaymentButton) {
            confirmPaymentButton.hidden = method === 'paypal';
        }

        if (method === 'paypal') {
            initializePayPalButtons();
        }

        setPaymentValidationMessage('');
    }

    async function ensurePayPalSdkLoaded() {
        if (!paypalEnabled) {
            setPaymentValidationMessage('PayPal no está configurado en el servidor. Completa ClientId y ClientSecret en appsettings.');
            return false;
        }

        if (window.paypal?.Buttons) {
            return true;
        }

        if (!payPalSdkPromise) {
            payPalSdkPromise = new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = `https://www.paypal.com/sdk/js?client-id=${encodeURIComponent(resolvedPayPalClientId)}&currency=USD&intent=capture`;
                script.async = true;
                script.onload = () => resolve(true);
                script.onerror = () => reject(new Error('No se pudo cargar PayPal SDK.'));
                document.head.appendChild(script);
            });
        }

        try {
            await payPalSdkPromise;
            return !!window.paypal?.Buttons;
        } catch {
            setPaymentValidationMessage('No se pudo cargar PayPal en este momento.');
            return false;
        }
    }

    async function initializePayPalButtons() {
        if (!paypalButtonsContainer) {
            return;
        }

        paypalButtonsContainer.innerHTML = '';
        paypalOrderId = '';

        if (!paypalEnabled) {
            setPaymentValidationMessage('PayPal no está configurado en el servidor. Completa ClientId y ClientSecret en appsettings.');
            return;
        }

        if (!paypalClientId) {
            setPaymentValidationMessage('Falta el ClientId de PayPal en la vista.');
            return;
        }

        const isReady = await ensurePayPalSdkLoaded();
        if (!isReady || !window.paypal?.Buttons) {
            return;
        }

        const buttons = window.paypal.Buttons({
            createOrder: async () => {
                try {
                    const response = await fetch('/Home/CreatePayPalOrder', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Requested-With': 'XMLHttpRequest'
                        },
                        body: JSON.stringify({ items: cartItems.map(item => ({ productId: item.productId, quantity: Number(item.quantity || 0) })) })
                    });

                    let payload = null;
                    try {
                        payload = await response.json();
                    } catch {
                        payload = null;
                    }

                    if (response.status === 401 || payload?.requiresLogin) {
                        showCartToast(payload?.errorMessage || 'Debes iniciar sesión para ir a pagar.', true);
                        window.setTimeout(() => {
                            window.location.href = payload?.loginUrl || loginUrl;
                        }, 500);
                        throw new Error('Usuario sin sesión activa');
                    }

                    if (!response.ok || !payload?.succeeded || !payload?.orderId) {
                        const message = payload?.errorMessage || 'No se pudo crear la orden de PayPal.';
                        setPaymentValidationMessage(message);
                        throw new Error(message);
                    }

                    paypalOrderId = payload.orderId;
                    return payload.orderId;
                } catch {
                    setPaymentValidationMessage('No se pudo iniciar PayPal. Verifica sesión, credenciales y vuelve a intentar.');
                    throw new Error('No se pudo iniciar PayPal.');
                }
            },
            onApprove: async data => {
                paypalOrderId = `${data?.orderID || paypalOrderId}`.trim();
                await submitPayPalCheckout();
            },
            onError: () => {
                setPaymentValidationMessage('No se pudo completar el flujo de PayPal. Inténtalo nuevamente.');
            },
            onCancel: () => {
                setPaymentValidationMessage('El pago con PayPal fue cancelado.');
            }
        });

        if (!buttons || typeof buttons.render !== 'function') {
            setPaymentValidationMessage('No se pudo inicializar el botón de PayPal.');
            return;
        }

        try {
            await buttons.render('#paypalButtonsContainer');
        } catch {
            setPaymentValidationMessage('No se pudo renderizar el botón de PayPal. Verifica tus credenciales.');
        }
    }

    function openPaymentModal() {
        if (!paymentModalOverlay) {
            return;
        }

        paypalOrderId = '';
        setPaymentValidationMessage('');
        paymentModalOverlay.hidden = false;
        document.body.classList.add('modal-open');

        if (selectedPaymentMethod === 'paypal') {
            initializePayPalButtons();
        }
    }

    function closePaymentModal() {
        if (!paymentModalOverlay) {
            return;
        }

        paymentModalOverlay.hidden = true;
        document.body.classList.remove('modal-open');
        setPaymentValidationMessage('');
        paypalOrderId = '';
    }

    function resolveDesiredQuantity(productId, triggerElement) {
        const triggerNode = triggerElement instanceof Element ? triggerElement : null;
        const nearestContainer = triggerNode?.closest('.product-footer, .modal-details');
        const scopedInput = nearestContainer?.querySelector(`[data-qty-input="${productId}"]`);
        const globalInput = document.querySelector(`[data-qty-input="${productId}"]`);
        const qtyInput = scopedInput || globalInput;
        clampCatalogQuantityInput(qtyInput);
        const rawValue = Number(qtyInput?.value || 1);
        if (!Number.isFinite(rawValue)) {
            return 1;
        }

        return Math.max(1, Math.floor(rawValue));
    }

    function addToCart(id, desiredQuantity = 1) {
        const product = findProduct(id);
        if (!product || product.stock <= 0) {
            showCartToast('Producto sin stock disponible.', true);
            return;
        }

        const normalizedId = `${id}`;
        const existing = cartItems.find(item => `${item.productId}` === normalizedId);
        const safeDesiredQuantity = Math.max(1, Math.floor(Number(desiredQuantity || 1)));
        if (existing) {
            if (existing.quantity >= product.stock) {
                showCartToast(`Solo quedan ${product.stock} unidades de ${product.name}.`, true);
                return;
            }

            const allowedIncrease = product.stock - existing.quantity;
            const quantityToAdd = Math.min(safeDesiredQuantity, allowedIncrease);
            existing.quantity += quantityToAdd;

            if (quantityToAdd < safeDesiredQuantity) {
                showCartToast(`Solo se agregaron ${quantityToAdd} unidad(es) por límite de stock.`, true);
            }
        } else {
            const initialQuantity = Math.min(safeDesiredQuantity, product.stock);
            cartItems.push({ productId: normalizedId, quantity: initialQuantity });

            if (initialQuantity < safeDesiredQuantity) {
                showCartToast(`Solo se agregaron ${initialQuantity} unidad(es) por límite de stock.`, true);
            }
        }

        renderCartPanel();
        updateCartBadge();
        showCartToast(`${product.name} agregado al carrito.`);
        addNotification(`Se agregó el producto "${product.name}" al carrito.`);
    }

    function buildCheckoutPayload() {
        const payload = {
            items: cartItems.map(item => ({ productId: item.productId, quantity: Number(item.quantity || 0) })),
            paymentMethod: selectedPaymentMethod,
            card: null,
            paypal: null,
            cash: null
        };

        if (selectedPaymentMethod === 'tarjeta') {
            const holderName = `${paymentCardHolder?.value || ''}`.trim();
            const cardNumber = `${paymentCardNumber?.value || ''}`.replace(/\s+/g, '').trim();
            const month = Number(paymentCardMonth?.value || 0);
            const year = Number(paymentCardYear?.value || 0);
            const cvv = `${paymentCardCvv?.value || ''}`.trim();

            if (!holderName) {
                return { error: 'El nombre del titular es obligatorio.' };
            }

            if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/.test(holderName)) {
                return { error: 'El nombre del titular contiene caracteres inválidos.' };
            }

            if (!/^\d{13,19}$/.test(cardNumber)) {
                return { error: 'El número de tarjeta es inválido.' };
            }

            if (month < 1 || month > 12) {
                return { error: 'El mes de vencimiento de la tarjeta es inválido.' };
            }

            const currentYear = new Date().getFullYear();
            const currentMonth = new Date().getMonth() + 1;
            if (year < currentYear || year > currentYear + 20) {
                return { error: 'El año de vencimiento de la tarjeta es inválido.' };
            }

            if (year === currentYear && month < currentMonth) {
                return { error: 'La tarjeta ya está vencida.' };
            }

            if (!/^\d{3,4}$/.test(cvv)) {
                return { error: 'El CVV de la tarjeta es inválido.' };
            }

            payload.card = {
                holderName,
                number: cardNumber,
                expiryMonth: month,
                expiryYear: year,
                cvv
            };

            return { payload };
        }

        if (selectedPaymentMethod === 'paypal') {
            if (!paypalOrderId) {
                return { error: 'Debes aprobar el pago con PayPal para continuar.' };
            }

            payload.paypal = { orderId: paypalOrderId };
            return { payload };
        }

        const customerName = `${paymentCashCustomerName?.value || ''}`.trim();
        if (!customerName) {
            return { error: 'El nombre para pago en efectivo es obligatorio.' };
        }

        if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$/.test(customerName)) {
            return { error: 'El nombre para pago en efectivo contiene caracteres inválidos.' };
        }

        payload.cash = { customerName };
        return { payload };
    }

    async function submitPayPalCheckout() {
        if (!paypalOrderId) {
            setPaymentValidationMessage('No se encontró una orden PayPal aprobada.');
            return;
        }

        const payload = {
            items: cartItems.map(item => ({ productId: item.productId, quantity: Number(item.quantity || 0) })),
            paymentMethod: 'paypal',
            paypal: {
                orderId: paypalOrderId
            }
        };

        try {
            const response = await fetch('/Home/CheckoutCart', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(payload)
            });

            const checkoutPayload = await response.json();
            if (response.status === 401 || checkoutPayload?.requiresLogin) {
                showCartToast(checkoutPayload?.errorMessage || 'Debes iniciar sesión para ir a pagar.', true);
                window.setTimeout(() => {
                    window.location.href = checkoutPayload?.loginUrl || loginUrl;
                }, 500);
                return;
            }

            if (!response.ok || !checkoutPayload?.succeeded) {
                setPaymentValidationMessage(checkoutPayload?.errorMessage || 'No se pudo confirmar la compra con PayPal.');
                return;
            }

            if (checkoutPayload?.receipt) {
                upsertReceipt(checkoutPayload.receipt);
            }

            paypalOrderId = '';
            cartItems = [];
            renderCartPanel();
            updateCartBadge();
            await refreshCatalogProducts();
            showCartToast(checkoutPayload.successMessage || 'Compra confirmada con PayPal.');
            addNotification(`Se realizó una compra correctamente${checkoutPayload?.receipt?.receiptNumber ? ` (${checkoutPayload.receipt.receiptNumber})` : ''}.`);
            closePaymentModal();
            closeCartPanel();
        } catch {
            setPaymentValidationMessage('No se pudo confirmar la compra con PayPal.');
        }
    }

    function checkoutCart() {
        if (!cartItems.length) {
            return;
        }

        openPaymentModal();
    }

    async function confirmCheckoutWithPayment() {
        if (!cartItems.length) {
            setPaymentValidationMessage('Tu carrito está vacío.');
            return;
        }

        const checkoutData = buildCheckoutPayload();
        if (checkoutData.error) {
            setPaymentValidationMessage(checkoutData.error);
            return;
        }

        if (!confirmPaymentButton) {
            return;
        }

        confirmPaymentButton.setAttribute('disabled', 'disabled');
        confirmPaymentButton.textContent = 'Procesando...';
        setPaymentValidationMessage('');

        try {
            const response = await fetch('/Home/CheckoutCart', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(checkoutData.payload)
            });

            const payload = await response.json();
            if (response.status === 401 || payload?.requiresLogin) {
                showCartToast(payload?.errorMessage || 'Debes iniciar sesión para ir a pagar.', true);
                window.setTimeout(() => {
                    window.location.href = payload?.loginUrl || loginUrl;
                }, 500);
                return;
            }

            if (!response.ok || !payload?.succeeded) {
                setPaymentValidationMessage(payload?.errorMessage || 'No se pudo completar la compra.');
                return;
            }

            if (payload?.receipt) {
                upsertReceipt(payload.receipt);
            }

            cartItems = [];
            renderCartPanel();
            updateCartBadge();
            await refreshCatalogProducts();
            showCartToast(payload.successMessage || 'Compra confirmada correctamente.');
            addNotification(`Se realizó una compra correctamente${payload?.receipt?.receiptNumber ? ` (${payload.receipt.receiptNumber})` : ''}.`);
            closePaymentModal();
            closeCartPanel();
        } catch {
            setPaymentValidationMessage('No se pudo completar la compra.');
        } finally {
            confirmPaymentButton.removeAttribute('disabled');
            confirmPaymentButton.textContent = 'Confirmar compra';
        }
    }

    closeModal?.addEventListener('click', closeProductModal);

    window.addEventListener('click', e => {
        if (e.target === modal) {
            closeProductModal();
        }
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && modal?.style.display === 'flex') {
            closeProductModal();
            return;
        }

        if (event.key === 'Escape' && paymentModalOverlay && !paymentModalOverlay.hidden) {
            closePaymentModal();
        }

        if (event.key === 'Escape') {
            closeOrderHistoryPanel();
            closeSupportPanel();
            closeNotificationsMenu();
        }
    });

    document.addEventListener('click', e => {
        const addButton = e.target.closest('[data-add]');
        if (!addButton) {
            return;
        }

        e.stopPropagation();
        const id = addButton.dataset.add;
        const desiredQuantity = resolveDesiredQuantity(id, addButton);
        addToCart(id, desiredQuantity);
    });

    document.addEventListener('input', event => {
        const qtyInput = event.target.closest('[data-qty-input]');
        if (!qtyInput) {
            return;
        }

        clampCatalogQuantityInput(qtyInput);
    });

    document.addEventListener('change', event => {
        const qtyInput = event.target.closest('[data-qty-input]');
        if (!qtyInput) {
            return;
        }

        clampCatalogQuantityInput(qtyInput);
    });

    cartPanelBody?.addEventListener('change', event => {
        const input = event.target.closest('[data-cart-qty]');
        if (!input) {
            return;
        }

        const productId = input.dataset.cartQty;
        const product = findProduct(productId);
        if (!product) {
            return;
        }

        const newQuantity = Math.max(1, Math.min(Number(input.value || 1), product.stock));
        const item = cartItems.find(cartItem => `${cartItem.productId}` === `${productId}`);
        if (!item) {
            return;
        }

        item.quantity = newQuantity;
        renderCartPanel();
    });

    cartPanelBody?.addEventListener('click', event => {
        const removeButton = event.target.closest('[data-cart-remove]');
        if (!removeButton) {
            return;
        }

        const productId = removeButton.dataset.cartRemove;
        cartItems = cartItems.filter(item => `${item.productId}` !== `${productId}`);
        renderCartPanel();
    });

    openCartPanelButton?.addEventListener('click', openCartPanel);
    closeCartPanelButton?.addEventListener('click', closeCartPanel);
    cartOverlay?.addEventListener('click', closeCartPanel);
    openOrderHistoryPanelButton?.addEventListener('click', openOrderHistoryPanel);
    closeOrderHistoryPanelButton?.addEventListener('click', closeOrderHistoryPanel);
    orderHistoryOverlay?.addEventListener('click', closeOrderHistoryPanel);
    openSupportPanelButton?.addEventListener('click', openSupportPanel);
    closeSupportPanelButton?.addEventListener('click', closeSupportPanel);
    supportOverlay?.addEventListener('click', closeSupportPanel);
    checkoutCartButton?.addEventListener('click', checkoutCart);
    closePaymentModalButton?.addEventListener('click', closePaymentModal);
    cancelPaymentButton?.addEventListener('click', closePaymentModal);
    confirmPaymentButton?.addEventListener('click', confirmCheckoutWithPayment);
    paymentModalOverlay?.addEventListener('click', event => {
        if (event.target === paymentModalOverlay) {
            closePaymentModal();
        }
    });
    paymentMethodButtons.forEach(button => {
        button.addEventListener('click', () => {
            const method = button.dataset.paymentMethod;
            if (!method) {
                return;
            }

            setPaymentMethod(method);
        });
    });
    setPaymentMethod(selectedPaymentMethod);

    notificationToggle?.addEventListener('click', event => {
        event.stopPropagation();
        const willOpen = !notificationsMenu?.classList.contains('open');
        if (willOpen) {
            openNotificationsMenu();
        } else {
            closeNotificationsMenu();
        }
    });

    notificationsMarkRead?.addEventListener('click', () => {
        markAllNotificationsAsRead();
    });

    function submitProductSearch() {
        currentPage = 1;
        renderProducts(currentFilter, catalogSearchInput?.value || '');
    }

    function applyCatalogPriceFilters() {
        const minValue = parsePriceFilterValue(catalogMinPriceInput?.value);
        const maxValue = parsePriceFilterValue(catalogMaxPriceInput?.value);

        if (minValue !== null && maxValue !== null && minValue > maxValue) {
            currentMinPrice = `${maxValue}`;
            currentMaxPrice = `${minValue}`;
            if (catalogMinPriceInput) {
                catalogMinPriceInput.value = `${maxValue}`;
            }
            if (catalogMaxPriceInput) {
                catalogMaxPriceInput.value = `${minValue}`;
            }
        } else {
            currentMinPrice = minValue === null ? '' : `${minValue}`;
            currentMaxPrice = maxValue === null ? '' : `${maxValue}`;
        }

        currentPage = 1;
        renderProducts(currentFilter, catalogSearchInput?.value || currentSearchTerm);
    }

    catalogSearchInput?.addEventListener('input', () => {
        window.clearTimeout(productSearchDebounceTimer);
        productSearchDebounceTimer = window.setTimeout(submitProductSearch, 150);
    });

    catalogSearchInput?.addEventListener('keydown', event => {
        if (event.key !== 'Enter') {
            return;
        }

        event.preventDefault();
        window.clearTimeout(productSearchDebounceTimer);
        submitProductSearch();
    });

    catalogSearchButton?.addEventListener('click', submitProductSearch);

    catalogSortSelect?.addEventListener('change', event => {
        currentSortOption = `${event.target?.value || 'relevance'}`;
        currentPage = 1;
        renderProducts(currentFilter, catalogSearchInput?.value || currentSearchTerm);
    });

    catalogApplyPriceFilterButton?.addEventListener('click', applyCatalogPriceFilters);

    catalogClearPriceFilterButton?.addEventListener('click', () => {
        currentMinPrice = '';
        currentMaxPrice = '';
        if (catalogMinPriceInput) {
            catalogMinPriceInput.value = '';
        }
        if (catalogMaxPriceInput) {
            catalogMaxPriceInput.value = '';
        }
        currentPage = 1;
        renderProducts(currentFilter, catalogSearchInput?.value || currentSearchTerm);
    });

    [catalogMinPriceInput, catalogMaxPriceInput].forEach(input => {
        input?.addEventListener('keydown', event => {
            if (event.key !== 'Enter') {
                return;
            }

            event.preventDefault();
            applyCatalogPriceFilters();
        });
    });

    document.querySelectorAll('.filter-chip').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.filter-chip').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentPage = 1;
            renderProducts(btn.dataset.filter || btn.textContent || 'Todo', catalogSearchInput?.value || '');
        });
    });

    async function refreshCatalogProducts() {
        try {
            const response = await fetch('/Home/GetCatalogProducts', {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const payload = await response.json();
            if (!response.ok || !payload?.succeeded || !Array.isArray(payload.products)) {
                return;
            }

            products = payload.products.map(mapCatalogProduct);
            hasLoadedCatalog = true;

            renderProducts(currentFilter, catalogSearchInput?.value || currentSearchTerm);
            renderCartPanel();
        } catch {
            if (!hasLoadedCatalog) {
                hasLoadedCatalog = true;
            }
            // Sin bloqueo de interfaz si falla una actualización automática.
        }
    }

    const track = document.getElementById('carouselTrack');
    const prevBtn = document.getElementById('carouselPrev');
    const nextBtn = document.getElementById('carouselNext');
    const scrollAmount = 300;

    prevBtn.addEventListener('click', () => {
        track.scrollBy({ left: -scrollAmount, behavior: 'smooth' });
    });

    nextBtn.addEventListener('click', () => {
        track.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    });

    function showSection(sectionName) {
        if (!storefrontSection || !clientProfileSection) {
            return;
        }

        const showProfile = sectionName === 'profile';
        storefrontSection.classList.toggle('active', !showProfile);
        clientProfileSection.classList.toggle('active', showProfile);
    }

    userProfileToggle?.addEventListener('click', event => {
        event.stopPropagation();
        const isOpen = userProfileMenu?.classList.toggle('open');
        userProfileToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    });

    goToMyProfile?.addEventListener('click', () => {
        userProfileMenu?.classList.remove('open');
        userProfileToggle?.setAttribute('aria-expanded', 'false');
        showSection('profile');
        clientProfileSection?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });

    goToStoreHome?.addEventListener('click', () => {
        userProfileMenu?.classList.remove('open');
        userProfileToggle?.setAttribute('aria-expanded', 'false');
        showSection('store');
        storefrontSection?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });

    backToStoreFromProfile?.addEventListener('click', () => {
        showSection('store');
        storefrontSection?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });

    function profileValidationMessage(field) {
        if (!field) {
            return 'Revisa los datos del perfil.';
        }

        if (field.validity.valueMissing) {
            if (field.name === 'FirstName') return 'El nombre es obligatorio.';
            if (field.name === 'LastName') return 'El apellido es obligatorio.';
        }

        if (field.validity.patternMismatch && field.name === 'Phone') {
            return 'El teléfono solo debe contener números.';
        }

        return 'Hay campos inválidos en el perfil.';
    }

    [profileFirstName, profileLastName, profilePhone].forEach(field => {
        field?.addEventListener('invalid', event => {
            const currentField = event.target;
            currentField.setCustomValidity(profileValidationMessage(currentField));
        });

        field?.addEventListener('input', event => {
            event.target.setCustomValidity('');
        });
    });

    profileForm?.addEventListener('submit', async event => {
        event.preventDefault();

        if (!profileForm.checkValidity()) {
            profileForm.querySelector(':invalid')?.reportValidity();
            return;
        }

        if (!profileSaveButton) {
            return;
        }

        profileSaveButton.setAttribute('disabled', 'disabled');
        profileSaveButton.textContent = 'Guardando...';

        try {
            const formData = new FormData(profileForm);
            const response = await fetch(profileForm.action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            let payload = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }

            if (!response.ok || !payload?.succeeded) {
                showCartToast(payload?.errorMessage || 'No fue posible actualizar tu perfil.', true);
                return;
            }

            if (typeof payload.profileImageUrl === 'string' && payload.profileImageUrl.trim()) {
                profileImageUrl.value = payload.profileImageUrl;
                profileImagePreview.innerHTML = `<img src="${payload.profileImageUrl}" alt="Foto de perfil" />`;
                if (profileAvatarLabel) {
                    profileAvatarLabel.innerHTML = `<img src="${payload.profileImageUrl}" alt="Foto de perfil" />`;
                }
                if (userHeaderAvatar) {
                    userHeaderAvatar.innerHTML = `<img src="${payload.profileImageUrl}" alt="Foto de perfil" />`;
                }
            }

            const userNameLabel = document.querySelector('.user-name');
            if (userNameLabel && typeof payload.displayName === 'string' && payload.displayName.trim()) {
                userNameLabel.textContent = payload.displayName;
            }

            showCartToast(payload.successMessage || 'Perfil actualizado correctamente.');
        } catch {
            showCartToast('No fue posible actualizar tu perfil.', true);
        } finally {
            profileSaveButton.removeAttribute('disabled');
            profileSaveButton.textContent = 'Guardar cambios';
        }
    });

    document.addEventListener('click', event => {
        const target = event.target;
        if (!(target instanceof Node)) {
            return;
        }

        if (userProfileMenu && userProfileToggle
            && !userProfileMenu.contains(target)
            && !userProfileToggle.contains(target)) {
            userProfileMenu.classList.remove('open');
            userProfileToggle.setAttribute('aria-expanded', 'false');
        }

        if (notificationsMenu && notificationToggle
            && !notificationsMenu.contains(target)
            && !notificationToggle.contains(target)) {
            closeNotificationsMenu();
        }
    });

    profileImageFile?.addEventListener('change', () => {
        const file = profileImageFile.files?.[0];
        if (!file || !profileImageUrl || !profileImagePreview) {
            return;
        }

        const reader = new FileReader();
        reader.onload = () => {
            const imageData = typeof reader.result === 'string' ? reader.result : '';
            if (!imageData) {
                return;
            }

            profileImageUrl.value = imageData;
            profileImagePreview.innerHTML = `<img src="${imageData}" alt="Foto de perfil" />`;
            if (profileAvatarLabel) {
                profileAvatarLabel.innerHTML = `<img src="${imageData}" alt="Foto de perfil" />`;
            }

            if (userHeaderAvatar) {
                userHeaderAvatar.innerHTML = `<img src="${imageData}" alt="Foto de perfil" />`;
            }
        };

        reader.readAsDataURL(file);
    });

    function setContactSupportMessage(message = '', isError = false) {
        if (!contactSupportMessage) {
            return;
        }

        const normalized = `${message || ''}`.trim();
        contactSupportMessage.textContent = normalized;
        contactSupportMessage.hidden = !normalized;
        contactSupportMessage.classList.toggle('error', isError);
        contactSupportMessage.classList.toggle('success', !isError && !!normalized);
    }

    function supportStatusClass(status) {
        return `${status || ''}`.toLowerCase() === 'resuelto' ? 'resolved' : 'pending';
    }

    function supportStatusLabel(status) {
        return `${status || ''}`.toLowerCase() === 'resuelto' ? 'Resuelto' : 'Pendiente';
    }

    function renderClientSupportInbox(conversations) {
        if (!clientSupportInbox) {
            return;
        }

        if (!Array.isArray(conversations) || !conversations.length) {
            clientSupportInbox.innerHTML = '<p class="client-tools-empty">Aún no tienes reportes o consultas registradas.</p>';
            return;
        }

        clientSupportInbox.innerHTML = conversations.map(conversation => {
            const messages = Array.isArray(conversation.messages) ? conversation.messages : [];
            return `
                <article class="support-thread-card">
                    <header class="support-thread-head">
                        <strong>${escapeHtml(conversation.subject || 'Sin asunto')}</strong>
                        <span class="support-thread-status ${supportStatusClass(conversation.status)}">${supportStatusLabel(conversation.status)}</span>
                    </header>
                    <div class="support-thread-meta">
                        <span>Actualizado: ${formatDateTime(conversation.updatedAtUtc)}</span>
                    </div>
                    <div class="support-thread-messages">
                        ${messages.filter(item => !item.isSystemEvent).map(item => `
                            <div class="support-thread-message ${`${item.senderRole || ''}`.toLowerCase() === 'administrador' ? 'admin' : 'client'}">
                                <div class="support-thread-message-head">
                                    <strong>${escapeHtml(item.senderName || 'Usuario')}</strong>
                                    <small>${formatDateTime(item.createdAtUtc)}</small>
                                </div>
                                <p>${escapeHtml(item.message || '')}</p>
                            </div>
                        `).join('')}
                    </div>
                    <form class="support-reply-form" data-conversation-id="${escapeHtml(conversation.conversationId)}">
                        <textarea name="Message" rows="3" maxlength="1500" placeholder="Escribe una respuesta para esta conversación" required></textarea>
                        <button type="submit" class="profile-save-btn">Responder</button>
                    </form>
                </article>
            `;
        }).join('');
    }

    async function loadClientSupportInbox() {
        if (!isAuthenticated || !clientSupportInbox || !clientSupportInboxLoadUrl) {
            return;
        }

        try {
            const response = await fetch(clientSupportInboxLoadUrl, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const payload = await response.json();
            if (!response.ok || !payload?.succeeded) {
                return;
            }

            const conversations = payload.conversations || [];
            processSupportResponseNotifications(conversations);
            renderClientSupportInbox(conversations);
        } catch {
            // No bloquear la UI por fallas transitorias.
        }
    }

    clientSupportInbox?.addEventListener('submit', async event => {
        const replyForm = event.target.closest('.support-reply-form');
        if (!replyForm) {
            return;
        }

        event.preventDefault();
        if (!clientSupportInboxReplyUrl) {
            return;
        }

        const conversationId = `${replyForm.dataset.conversationId || ''}`.trim();
        const textarea = replyForm.querySelector('textarea[name="Message"]');
        const submitButton = replyForm.querySelector('button[type="submit"]');
        const replyMessage = `${textarea?.value || ''}`.trim();
        if (!conversationId || !replyMessage) {
            return;
        }

        submitButton?.setAttribute('disabled', 'disabled');

        try {
            const formData = new FormData();
            formData.append('ConversationId', conversationId);
            formData.append('Message', replyMessage);
            const antiForgeryToken = contactSupportForm?.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (antiForgeryToken) {
                formData.append('__RequestVerificationToken', antiForgeryToken);
            }

            const response = await fetch(clientSupportInboxReplyUrl, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const payload = await response.json();
            if (!response.ok || !payload?.succeeded) {
                setContactSupportMessage(payload?.errorMessage || 'No se pudo enviar tu respuesta.', true);
                return;
            }

            if (textarea) {
                textarea.value = '';
            }

            setContactSupportMessage(payload.successMessage || 'Tu respuesta fue enviada correctamente.');
            await loadClientSupportInbox();
        } catch {
            setContactSupportMessage('No se pudo enviar tu respuesta.', true);
        } finally {
            submitButton?.removeAttribute('disabled');
        }
    });

    contactSupportForm?.addEventListener('submit', async event => {
        event.preventDefault();
        setContactSupportMessage('');

        if (!contactSupportForm.checkValidity()) {
            contactSupportForm.querySelector(':invalid')?.reportValidity();
            return;
        }

        if (!contactSupportSubmitButton) {
            return;
        }

        contactSupportSubmitButton.setAttribute('disabled', 'disabled');
        contactSupportSubmitButton.textContent = 'Enviando...';

        try {
            const formData = new FormData(contactSupportForm);
            const reportSubject = `${formData.get('Subject') || ''}`.trim();
            const response = await fetch(contactSupportForm.action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const payload = await response.json();
            if (!response.ok || !payload?.succeeded) {
                setContactSupportMessage(payload?.errorMessage || 'No se pudo enviar tu consulta.', true);
                return;
            }

            setContactSupportMessage(payload.successMessage || 'Tu consulta fue enviada correctamente.');
            addNotification(`Se envió correctamente el reporte${reportSubject ? ` "${reportSubject}"` : ''}.`);
            contactSupportForm.reset();
            const emailField = contactSupportForm.querySelector('input[name="Email"]');
            const nameField = contactSupportForm.querySelector('input[name="Name"]');
            if (emailField) {
                emailField.value = `${window.portadaData?.userEmail || ''}`;
            }
            if (nameField) {
                nameField.value = `${window.portadaData?.userDisplayName || ''}`;
            }

            await loadClientSupportInbox();
        } catch {
            setContactSupportMessage('No se pudo enviar tu consulta.', true);
        } finally {
            contactSupportSubmitButton.removeAttribute('disabled');
            contactSupportSubmitButton.textContent = 'Enviar consulta';
        }
    });

    const currentSection = new URLSearchParams(window.location.search).get('section');
    showSection(currentSection === 'profile' ? 'profile' : 'store');
    loadNotifications();
    loadSupportResponseTracker();
    renderNotifications();
    loadCart();
    renderProducts();
    renderCartPanel();
    updateCartBadge();
    if (!products.length) {
        refreshCatalogProducts();
    }
    renderPurchaseHistory();
    loadPurchaseHistory();
    loadClientSupportInbox();
    window.setInterval(refreshCatalogProducts, 15000);
    if (isAuthenticated) {
        window.setInterval(loadClientSupportInbox, 25000);
    }
});
