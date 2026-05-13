import { PropsWithChildren } from 'react';
import "./Layout.css";
import { NavMenu } from './NavMenu';
import { BaseAxiosApiLoader } from '../../api/AxiosApiLoader';
import { useBranding } from '../../branding/BrandingContext';

export const Layout: React.FC<PropsWithChildren<{ apiLoader?: BaseAxiosApiLoader }>> = (props) => {

  const branding = useBranding();

  return (

    <div className="welcome page">
      {branding.bannerImageUrl &&
        <div
          className="brand-banner"
          style={{ backgroundColor: branding.themeColor, height: branding.bannerHeight }}
        >
          <img
            src={branding.bannerImageUrl}
            alt={branding.bannerAltText}
            style={{ maxHeight: branding.bannerHeight }}
          />
        </div>
      }
      <div className="narrow page-padding">
        {props.apiLoader && <NavMenu />}

        {props.children}

      </div>
    </div>
  );
}
