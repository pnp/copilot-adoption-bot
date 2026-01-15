import React from "react";
import * as AdaptiveCards from "adaptivecards";


export const AdaptiveCard: React.FC<{ json: string }> = (props) => {

  const containerRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (props.json && containerRef.current) {
      // Clear previous card
      containerRef.current.innerHTML = '';
      
      try {
        const card = new AdaptiveCards.AdaptiveCard();
        
        // Configure host config to allow full width
        card.hostConfig = new AdaptiveCards.HostConfig({
          supportsInteractivity: true,
          spacing: {
            small: 3,
            default: 8,
            medium: 20,
            large: 30,
            extraLarge: 40,
            padding: 10
          },
          actions: {
            maxActions: 5,
            spacing: 'default',
            buttonSpacing: 8,
            showCard: {
              actionMode: 'inline',
              inlineTopMargin: 8
            },
            actionsOrientation: 'horizontal',
            actionAlignment: 'stretch'
          }
        });
        
        card.parse(JSON.parse(props.json));
        const renderedCard = card.render();
        
        if (renderedCard) {
          // Set width to 100% to fill container
          renderedCard.style.width = '100%';
          renderedCard.style.maxWidth = '100%';
          renderedCard.style.boxSizing = 'border-box';
          
          // Also set width on all child containers
          const allDivs = renderedCard.querySelectorAll('div');
          allDivs.forEach((div: HTMLElement) => {
            div.style.maxWidth = '100%';
            div.style.boxSizing = 'border-box';
          });
          
          // Set tables (FactSets) to full width
          const allTables = renderedCard.querySelectorAll('table');
          allTables.forEach((table: HTMLElement) => {
            table.style.width = '100%';
            table.style.tableLayout = 'fixed';
          });
          
          // Set action containers to full width and wrap buttons
          const actionSets = renderedCard.querySelectorAll('.ac-actionSet');
          actionSets.forEach((actionSet) => {
            const el = actionSet as HTMLElement;
            el.style.width = '100%';
            el.style.display = 'flex';
            el.style.flexWrap = 'wrap';
            el.style.gap = '8px';
          });
          
          // Make action buttons flexible
          const actionButtons = renderedCard.querySelectorAll('.ac-pushButton');
          actionButtons.forEach((button) => {
            const el = button as HTMLElement;
            el.style.flex = '1 1 auto';
            el.style.minWidth = '0';
            el.style.maxWidth = '100%';
            el.style.overflow = 'hidden';
            el.style.textOverflow = 'ellipsis';
            el.style.whiteSpace = 'nowrap';
          });
          
          containerRef.current.appendChild(renderedCard);
        }
      } catch (e) {
        console.error('Error rendering adaptive card:', e);
      }
    }
  }, [props.json]);

  return (
    <div className="adaptiveCardContainer" style={{ width: '100%' }}>
      <div ref={containerRef} style={{ width: '100%' }}></div>
    </div>
  );
};
