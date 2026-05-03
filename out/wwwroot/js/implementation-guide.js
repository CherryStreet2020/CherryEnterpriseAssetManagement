(function() {
    'use strict';
    
    const STORAGE_KEY = 'cherryai_implementation_progress';
    
    let progress = loadProgress();
    
    function loadProgress() {
        try {
            const saved = localStorage.getItem(STORAGE_KEY);
            return saved ? JSON.parse(saved) : { checkedItems: [], completedSections: [], startDate: null };
        } catch {
            return { checkedItems: [], completedSections: [], startDate: null };
        }
    }
    
    function saveProgress() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(progress));
        } catch {}
    }
    
    function initProgress() {
        if (!progress.startDate) {
            progress.startDate = new Date().toISOString();
            saveProgress();
        }
        
        document.querySelectorAll('.impl-checklist input[type="checkbox"]').forEach(cb => {
            const id = cb.dataset.checkId;
            if (id && progress.checkedItems.includes(id)) {
                cb.checked = true;
                cb.closest('.impl-checklist-item')?.classList.add('completed');
            }
            
            cb.addEventListener('change', function() {
                const itemId = this.dataset.checkId;
                if (!itemId) return;
                
                if (this.checked) {
                    if (!progress.checkedItems.includes(itemId)) {
                        progress.checkedItems.push(itemId);
                    }
                    this.closest('.impl-checklist-item')?.classList.add('completed');
                } else {
                    progress.checkedItems = progress.checkedItems.filter(i => i !== itemId);
                    this.closest('.impl-checklist-item')?.classList.remove('completed');
                }
                saveProgress();
                updateProgressDisplay();
            });
        });
        
        updateProgressDisplay();
    }
    
    function updateProgressDisplay() {
        const totalItems = document.querySelectorAll('.impl-checklist input[type="checkbox"]').length;
        const checkedItems = progress.checkedItems.length;
        const percentage = totalItems > 0 ? Math.round((checkedItems / totalItems) * 100) : 0;
        
        const progressBar = document.getElementById('impl-progress-bar');
        const progressText = document.getElementById('impl-progress-text');
        const progressPercent = document.getElementById('impl-progress-percent');
        
        if (progressBar) {
            progressBar.style.width = percentage + '%';
            progressBar.style.background = percentage === 100 ? '#10b981' : 
                                           percentage > 50 ? '#3b82f6' : '#f59e0b';
        }
        if (progressText) {
            progressText.textContent = `${checkedItems} of ${totalItems} tasks completed`;
        }
        if (progressPercent) {
            progressPercent.textContent = percentage + '%';
        }
        
        document.querySelectorAll('.impl-nav-item').forEach(item => {
            const sectionId = item.dataset.section;
            if (!sectionId) return;
            
            const section = document.getElementById(sectionId);
            if (!section) return;
            
            const sectionChecks = section.querySelectorAll('.impl-checklist input[type="checkbox"]');
            const sectionTotal = sectionChecks.length;
            const sectionDone = Array.from(sectionChecks).filter(cb => cb.checked).length;
            
            const indicator = item.querySelector('.nav-progress-indicator');
            if (indicator) {
                if (sectionTotal === 0) {
                    indicator.className = 'nav-progress-indicator';
                } else if (sectionDone === sectionTotal) {
                    indicator.className = 'nav-progress-indicator complete';
                    indicator.innerHTML = '<i class="fas fa-check"></i>';
                } else if (sectionDone > 0) {
                    indicator.className = 'nav-progress-indicator in-progress';
                    indicator.textContent = `${sectionDone}/${sectionTotal}`;
                } else {
                    indicator.className = 'nav-progress-indicator';
                    indicator.textContent = sectionTotal;
                }
            }
        });
    }
    
    function initStickyNav() {
        const nav = document.getElementById('impl-sticky-nav');
        const content = document.querySelector('.impl-content');
        if (!nav || !content) return;
        
        const sections = document.querySelectorAll('.impl-section[id]');
        const navItems = document.querySelectorAll('.impl-nav-item');
        
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const id = entry.target.id;
                    navItems.forEach(item => {
                        item.classList.toggle('active', item.dataset.section === id);
                    });
                }
            });
        }, { rootMargin: '-20% 0px -60% 0px' });
        
        sections.forEach(section => observer.observe(section));
        
        navItems.forEach(item => {
            item.addEventListener('click', (e) => {
                e.preventDefault();
                const sectionId = item.dataset.section;
                const section = document.getElementById(sectionId);
                if (section) {
                    const headerOffset = 100;
                    const elementPosition = section.getBoundingClientRect().top;
                    const offsetPosition = elementPosition + window.pageYOffset - headerOffset;
                    
                    window.scrollTo({
                        top: offsetPosition,
                        behavior: 'smooth'
                    });
                }
            });
        });
    }
    
    function initExpandables() {
        document.querySelectorAll('.impl-expandable-header').forEach(header => {
            header.addEventListener('click', () => {
                const parent = header.closest('.impl-expandable');
                parent?.classList.toggle('expanded');
            });
        });
    }
    
    function initQuickLinks() {
        document.querySelectorAll('a[href^="/"]').forEach(link => {
            if (link.closest('.impl-content') && !link.classList.contains('no-quick-link')) {
                link.setAttribute('target', '_blank');
                link.classList.add('impl-quick-link');
            }
        });
    }
    
    function initSearch() {
        const searchInput = document.getElementById('impl-search');
        if (!searchInput) return;
        
        const allSections = document.querySelectorAll('.impl-section');
        const allSubsections = document.querySelectorAll('.impl-subsection');
        
        searchInput.addEventListener('input', function() {
            const query = this.value.toLowerCase().trim();
            
            if (!query) {
                allSections.forEach(s => s.style.display = '');
                allSubsections.forEach(s => s.style.display = '');
                return;
            }
            
            allSubsections.forEach(subsection => {
                const text = subsection.textContent.toLowerCase();
                subsection.style.display = text.includes(query) ? '' : 'none';
            });
            
            allSections.forEach(section => {
                const visibleSubs = section.querySelectorAll('.impl-subsection:not([style*="display: none"])');
                const headerMatches = section.querySelector('.impl-section-header')?.textContent.toLowerCase().includes(query);
                section.style.display = (visibleSubs.length > 0 || headerMatches) ? '' : 'none';
            });
        });
    }
    
    function initPrint() {
        const printBtn = document.getElementById('impl-print-btn');
        if (printBtn) {
            printBtn.addEventListener('click', () => window.print());
        }
    }
    
    function initResetProgress() {
        const resetBtn = document.getElementById('impl-reset-btn');
        if (resetBtn) {
            resetBtn.addEventListener('click', () => {
                if (confirm('Are you sure you want to reset all progress? This cannot be undone.')) {
                    progress = { checkedItems: [], completedSections: [], startDate: null };
                    saveProgress();
                    document.querySelectorAll('.impl-checklist input[type="checkbox"]').forEach(cb => {
                        cb.checked = false;
                        cb.closest('.impl-checklist-item')?.classList.remove('completed');
                    });
                    updateProgressDisplay();
                }
            });
        }
    }
    
    document.addEventListener('DOMContentLoaded', function() {
        initProgress();
        initStickyNav();
        initExpandables();
        initQuickLinks();
        initSearch();
        initPrint();
        initResetProgress();
    });
})();
