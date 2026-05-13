import React, { PropsWithChildren } from 'react';

import { Layout } from './components/app/Layout';
import { Dashboard } from './pages/Home/Dashboard';
import { LoginPopupMSAL } from './pages/Login/LoginPopupMSAL';
import { Redirect, Route } from "react-router-dom";
import { FluentProvider, teamsLightTheme, Theme } from '@fluentui/react-components';
import { LoginPopupTeams } from './pages/Login/LoginPopupTeams';
import { BaseAxiosApiLoader } from './api/AxiosApiLoader';
import { MessageTemplatesPage } from './pages/MessageTemplates/MessageTemplatesPage';
import { SendNudgePage } from './pages/SendNudge/SendNudgePage';
import { BatchProgressPage } from './pages/BatchProgress/BatchProgressPage';
import { BatchHistoryPage } from './pages/BatchHistory/BatchHistoryPage';
import { SettingsPage } from './pages/Settings/SettingsPage';
import { SmartGroupsPage } from './pages/SmartGroups/SmartGroupsPage';
import { useBranding } from './branding/BrandingContext';

export const AppRoutes: React.FC<PropsWithChildren<AppRoutesProps>> = (props) => {

    const branding = useBranding();

    const brandedTheme: Theme = React.useMemo(() => ({
        ...teamsLightTheme,
        colorBrandBackground: branding.themeColor,
        colorBrandBackgroundHover: branding.themeColor,
        colorBrandBackgroundPressed: branding.themeColor,
        colorBrandBackgroundSelected: branding.themeColor,
        colorCompoundBrandBackground: branding.themeColor,
        colorCompoundBrandBackgroundHover: branding.themeColor,
        colorCompoundBrandBackgroundPressed: branding.themeColor,
        colorBrandStroke1: branding.themeColor,
        colorCompoundBrandStroke: branding.themeColor,
        colorCompoundBrandStrokeHover: branding.themeColor,
        colorCompoundBrandStrokePressed: branding.themeColor,
        colorBrandForeground1: branding.themeColor,
        colorBrandForeground2: branding.themeColor,
        colorBrandForegroundLink: branding.themeColor,
        colorBrandForegroundLinkHover: branding.themeColor,
        colorBrandForegroundLinkPressed: branding.themeColor,
    }), [branding.themeColor]);

    return (
        <FluentProvider theme={brandedTheme}>
            {props.apiLoader ?
                (
                    <Layout apiLoader={props.apiLoader}>
                        <Route exact path="/">
                            <Redirect to="/tabhome" />
                        </Route>
                        <Route exact path='/tabhome' render={() => <Dashboard loader={props.apiLoader} />} />
                        <Route exact path='/templates' render={() => <MessageTemplatesPage loader={props.apiLoader} />} />
                        <Route exact path='/sendnudge' render={() => <SendNudgePage loader={props.apiLoader} />} />
                        <Route exact path='/smartgroups' render={() => <SmartGroupsPage loader={props.apiLoader} />} />
                        <Route exact path='/batchhistory' render={() => <BatchHistoryPage loader={props.apiLoader} />} />
                        <Route exact path='/batch/:batchId' render={() => <BatchProgressPage loader={props.apiLoader} />} />
                        <Route exact path='/settings' render={() => <SettingsPage loader={props.apiLoader} />} />
                    </Layout>
                )
                :
                (
                    <Layout>
                        <Route exact path="/">
                            {props.loginMethod === LoginMethod.MSAL &&
                                <LoginPopupMSAL />
                            }
                            {props.loginMethod === LoginMethod.TeamsSSO &&
                                <LoginPopupTeams onAuthReload={props.onAuthReload} />
                            }
                        </Route>
                    </Layout>
                )}
        </FluentProvider>
    );
}
interface AppRoutesProps {
    apiLoader?: BaseAxiosApiLoader,
    loginMethod?: LoginMethod,
    onAuthReload: Function,
    theme: Theme
}

export enum LoginMethod {
    MSAL,
    TeamsSSO
}
