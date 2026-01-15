import { Pie } from 'react-chartjs-2';

import React from 'react';
import { ColourPicker } from '../../utils/ColourPicker';

export const RandomChart: React.FC<{}> = () => {

  const data = {
    labels: ['Yes', 'No', 'Maybe'],
    datasets: [
      {
        data: [2, 3, 8],
        backgroundColor: [
            ColourPicker.chartColours[0],
            ColourPicker.chartColours[1],
            ColourPicker.chartColours[2]
        ],
        borderColor: [
          '#002050',
        ],
        borderWidth: 1,
      }
    ],
    plugins: {
      legend: {
          display: true,
      }
  }
  };
  return (
    <div>

      <Pie data={data} options={ { plugins:{ legend: {display: false }}, maintainAspectRatio: false}} />

    </div>
  );
};

