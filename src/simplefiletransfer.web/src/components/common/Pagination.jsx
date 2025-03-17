import React from 'react';

export function Pagination({ currentPage, totalPages, onPageChange }) {
  if (totalPages <= 1) return null;

  return (
    <div className="pagination">
      <style>
        {`
          .pagination {
            display: flex;
            justify-content: center;
            align-items: center;
            margin-top: 1rem;
            gap: 0.5rem;
          }
          
          .pagination-button {
            background-color: var(--bg-highlight);
            color: var(--text);
            border: 1px solid var(--border-color);
            border-radius: 4px;
            padding: 0.3rem 0.6rem;
            font-size: 0.8rem;
            cursor: pointer;
            min-width: 32px;
            text-align: center;
          }
          
          .pagination-button:hover {
            background-color: var(--light-bg);
          }
          
          .pagination-button.active {
            background-color: var(--primary-color);
            color: var(--bg);
            border-color: var(--primary-color);
          }
          
          .pagination-button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
          }
          
          .pagination-info {
            color: var(--dim);
            font-size: 0.8rem;
            margin: 0 0.5rem;
          }
        `}
      </style>
      
      <button 
        className="pagination-button"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={currentPage === 1}
      >
        &lt;
      </button>
      
      <span className="pagination-info">
        Page {currentPage} of {totalPages}
      </span>
      
      <button 
        className="pagination-button"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={currentPage === totalPages}
      >
        &gt;
      </button>
    </div>
  );
} 