(function() {
    'use strict';

    const siteSelect = document.querySelector('[data-wr-site]');
    const locationSelect = document.querySelector('[data-wr-location]');
    const assetSelect = document.querySelector('[data-wr-asset]');
    const filterForm = document.getElementById('filterForm');

    if (!siteSelect || !filterForm) return;

    function setLoading(select, isLoading, message) {
        if (!select) return;
        select.disabled = isLoading;
        if (isLoading) {
            select.innerHTML = '<option value="">' + (message || 'Loading...') + '</option>';
        }
    }

    function populateSelect(select, items, placeholder) {
        if (!select) return;
        select.innerHTML = '<option value="">' + placeholder + '</option>';
        items.forEach(function(item) {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.label;
            select.appendChild(option);
        });
        select.disabled = false;
    }

    function setError(select, message) {
        if (!select) return;
        select.innerHTML = '<option value="">' + message + '</option>';
        select.disabled = true;
    }

    function setEmpty(select, message) {
        if (!select) return;
        select.innerHTML = '<option value="">' + message + '</option>';
        select.disabled = true;
    }

    function updateUrl(siteId, locationId) {
        const url = new URL(window.location.href);
        if (siteId) {
            url.searchParams.set('SelectedSiteId', siteId);
        } else {
            url.searchParams.delete('SelectedSiteId');
        }
        if (locationId) {
            url.searchParams.set('SelectedLocationId', locationId);
        } else {
            url.searchParams.delete('SelectedLocationId');
        }
        history.replaceState(null, '', url.toString());
    }

    async function fetchLocations(siteId) {
        const response = await fetch('Create?handler=LocationsJson&siteId=' + siteId);
        if (!response.ok) throw new Error('Failed to load locations');
        return await response.json();
    }

    async function fetchAssets(siteId, locationId) {
        let url = 'Create?handler=AssetsJson&siteId=' + siteId;
        if (locationId) {
            url += '&locationId=' + locationId;
        }
        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load assets');
        return await response.json();
    }

    async function onSiteChange(e) {
        e.preventDefault();
        const siteId = siteSelect.value;

        if (!siteId) {
            if (locationSelect) setEmpty(locationSelect, '-- Select a site first --');
            if (assetSelect) setEmpty(assetSelect, '-- Select a site first --');
            updateUrl(null, null);
            return;
        }

        if (locationSelect) setLoading(locationSelect, true, 'Loading locations...');
        if (assetSelect) setLoading(assetSelect, true, 'Loading assets...');

        try {
            const [locations, assets] = await Promise.all([
                fetchLocations(siteId),
                fetchAssets(siteId, null)
            ]);

            if (locationSelect) {
                if (locations.length > 0) {
                    populateSelect(locationSelect, locations, '-- All Locations --');
                } else {
                    setEmpty(locationSelect, '-- No locations for this site --');
                }
            }

            if (assetSelect) {
                if (assets.length > 0) {
                    populateSelect(assetSelect, assets, '-- Select or leave blank for Smart Assist --');
                } else {
                    setEmpty(assetSelect, '-- No assets in this site --');
                }
            }

            updateUrl(siteId, null);
        } catch (err) {
            console.error('Error loading cascade data:', err);
            if (locationSelect) setError(locationSelect, '-- Unable to load — refresh page --');
            if (assetSelect) setError(assetSelect, '-- Unable to load — refresh page --');
        }
    }

    async function onLocationChange(e) {
        e.preventDefault();
        const siteId = siteSelect.value;
        const locationId = locationSelect ? locationSelect.value : null;

        if (!siteId) return;

        if (assetSelect) setLoading(assetSelect, true, 'Loading assets...');

        try {
            const assets = await fetchAssets(siteId, locationId || null);

            if (assetSelect) {
                if (assets.length > 0) {
                    populateSelect(assetSelect, assets, '-- Select or leave blank for Smart Assist --');
                } else {
                    setEmpty(assetSelect, '-- No assets in this location --');
                }
            }

            updateUrl(siteId, locationId);
        } catch (err) {
            console.error('Error loading assets:', err);
            if (assetSelect) setError(assetSelect, '-- Unable to load — refresh page --');
        }
    }

    siteSelect.addEventListener('change', onSiteChange);
    if (locationSelect) {
        locationSelect.addEventListener('change', onLocationChange);
    }

    filterForm.addEventListener('submit', function(e) {
        e.preventDefault();
    });
})();
