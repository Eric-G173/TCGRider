
 import React from 'react';
 import styles from './DefaultGrid.module.css';
 
 function EmptyGrid() { 
  return (
  <div className={styles['tracker-empty']}>
              <p>Select a tracker to view cards</p>
            </div>
  )
 }

 export default EmptyGrid