import { useHistory, useLocation } from 'react-router-dom';
import { TabValue, TabList, Tab, SelectTabData, SelectTabEvent } from '@fluentui/react-components';
import { useState, useEffect } from 'react';

export function NavMenu() {

  const [selectedValue, setSelectedValue] = useState<TabValue>("home");
  const history = useHistory();
  const location = useLocation();

  useEffect(() => {
    const path = location.pathname;
    if (path === '/tabhome') {
      setSelectedValue('home');
    } else if (path === '/templates') {
      setSelectedValue('templates');
    } else if (path === '/sendnudge') {
      setSelectedValue('sendnudge');
    } else if (path === '/smartgroups') {
      setSelectedValue('smartgroups');
    } else if (path === '/batchhistory') {
      setSelectedValue('batchhistory');
    } else if (path === '/settings') {
      setSelectedValue('settings');
    }
  }, [location.pathname]);

  const onTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
    setSelectedValue(data.value);
    if (data.value === "home") {
      history.push('/tabhome');
    } else if (data.value === "templates") {
      history.push('/templates');
    } else if (data.value === "sendnudge") {
      history.push('/sendnudge');
    } else if (data.value === "smartgroups") {
      history.push('/smartgroups');
    } else if (data.value === "batchhistory") {
      history.push('/batchhistory');
    } else if (data.value === "settings") {
      history.push('/settings');
    }
  };

  return (
    <div className='nav'>
      <TabList selectedValue={selectedValue} onTabSelect={onTabSelect}>
        <Tab id="Home" value="home">
          Home
        </Tab>
        <Tab id="Templates" value="templates">
          Message Templates
        </Tab>
        <Tab id="SendNudge" value="sendnudge">
          Send Nudge
        </Tab>
        <Tab id="SmartGroups" value="smartgroups">
          Smart Groups
        </Tab>
        <Tab id="BatchHistory" value="batchhistory">
          Batch History
        </Tab>
        <Tab id="Settings" value="settings">
          Settings
        </Tab>
      </TabList>
    </div>
  );

}
