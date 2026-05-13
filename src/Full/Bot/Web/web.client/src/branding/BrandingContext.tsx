import React, { createContext, PropsWithChildren, useContext, useEffect, useState } from "react";
import { BrandingConfig, defaultBranding, loadBranding } from "./branding";

const BrandingContext = createContext<BrandingConfig>(defaultBranding);

export const useBranding = (): BrandingConfig => useContext(BrandingContext);

export const BrandingProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
    const [branding, setBranding] = useState<BrandingConfig>(defaultBranding);

    useEffect(() => {
        let cancelled = false;
        loadBranding().then((b) => {
            if (!cancelled) {
                setBranding(b);
            }
        });
        return () => {
            cancelled = true;
        };
    }, []);

    useEffect(() => {
        if (branding.appTitle) {
            document.title = branding.appTitle;
        }
        // Expose theme color as a CSS variable for use in plain CSS files.
        document.documentElement.style.setProperty("--brand-color", branding.themeColor);
    }, [branding.appTitle, branding.themeColor]);

    return <BrandingContext.Provider value={branding}>{children}</BrandingContext.Provider>;
};
