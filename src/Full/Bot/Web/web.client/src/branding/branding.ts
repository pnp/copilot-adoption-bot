// Client-side branding configuration.
// The shape of /branding.json served from the web.client `public/` folder.
// Customers can edit branding.json to override theme color, banner image, and title
// without needing to rebuild the app.

export interface BrandingConfig {
    appTitle: string;
    themeColor: string;
    bannerImageUrl: string;
    bannerAltText: string;
    bannerHeight: number;
}

export const defaultBranding: BrandingConfig = {
    appTitle: "Copilot Adoption Bot",
    themeColor: "#6264a7",
    bannerImageUrl: "",
    bannerAltText: "Copilot Adoption Bot",
    bannerHeight: 80,
};

export async function loadBranding(): Promise<BrandingConfig> {
    try {
        // Use BASE_URL so this works behind a sub-path (Vite `base`).
        const base = (import.meta.env.BASE_URL ?? "/").replace(/\/$/, "");
        const response = await fetch(`${base}/branding.json`, { cache: "no-cache" });
        if (!response.ok) {
            console.warn(`branding.json not found (status ${response.status}). Using defaults.`);
            return defaultBranding;
        }
        const data = (await response.json()) as Partial<BrandingConfig>;
        return { ...defaultBranding, ...data };
    } catch (err) {
        console.warn("Failed to load branding.json. Using defaults.", err);
        return defaultBranding;
    }
}
