using System.Collections.Concurrent;
using System.Collections.Generic;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
namespace com.lightstreamer.util
{
    public class Matrix<R, C, V>
    {
        private ConcurrentDictionary<R, ConcurrentDictionary<C, V>> matrix = new ConcurrentDictionary<R, ConcurrentDictionary<C, V>>();

        /// <summary>
        /// Inserts an element in the matrix. If another element is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insert(V value, R row, C column)
        {
            ConcurrentDictionary<C, V> matrixRow;
            bool presente = matrix.TryGetValue(row, out matrixRow);
            if (!presente)
            {
                matrixRow = new ConcurrentDictionary<C, V>();
                matrix[row] = matrixRow;
            }

            matrixRow[column] = value;
        }

        /// <summary>
        /// Gets the element at the specified position in the matrix. If the position is empty null is returned.
        /// </summary>
        public virtual V get(R row, C column)
        {
            ConcurrentDictionary<C, V> matrixRow;
            matrix.TryGetValue(row, out matrixRow);
            if (matrixRow != null)
            {
                V col;
                matrixRow.TryGetValue(column, out col);

                return col;
            }
            return default(V);
        }

        /// <summary>
        /// Removes the element at the specified position in the matrix.
        /// </summary>
        public virtual void del(R row, C column)
        {
            ConcurrentDictionary<C, V> matrixRow;
            matrix.TryGetValue(row, out matrixRow);
            if (matrixRow == null)
            {
                return;
            }
            V value;
            matrixRow.TryRemove(column, out value);
            if (matrixRow.Count == 0)
            {
                ConcurrentDictionary<C, V> removed;
                //row is empty, get rid of it
                matrix.TryRemove(row, out removed);
            }
        }

        /// <summary>
        /// Inserts a full row in the matrix. If another row is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insertRow(ConcurrentDictionary<C, V> insRow, R row)
        {
            matrix[row] = insRow;
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual ConcurrentDictionary<C, V> getRow(R row)
        {
            ConcurrentDictionary<C, V> rowi;
            matrix.TryGetValue(row, out rowi);

            return rowi;
        }

        /// <summary>
        /// Removes the row at the specified position in the matrix.
        /// </summary>
        public virtual void delRow(R row)
        {
            ConcurrentDictionary<C, V> rowi;
            matrix.TryRemove(row, out rowi);
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual ConcurrentDictionary<R, ConcurrentDictionary<C, V>> EntireMatrix
        {
            get
            {
                return this.matrix;
            }
        }

        /// <summary>
        /// Verify if there are elements in the grid
        /// </summary>
        public virtual bool Empty
        {
            get
            {
                return matrix.Count == 0;
            }
        }

        /// <summary>
        /// Return current number of elements in the grid
        /// </summary>
        public virtual int Count(R row)
        {

            return matrix[row].Count;
        }

        /// <summary>
        /// Executes a given callback passing each element of the Matrix. The callback
        /// receives the element together with its coordinates. <BR>  
        /// Callbacks are executed synchronously before the method returns: calling 
        /// insert or delete methods during callback execution may result in 
        /// a wrong iteration, return false from the callback to remove the current element.
        /// </summary>
        public virtual void forEachElement(ElementCallback<R, C, V> callback)
        {
            foreach (R row in this.matrix.Keys)
            {
                this.forEachElementInRow(row, callback);
            }
        }

        /*
         * Executes a given callback passing the key of each row containing at least one element.
         */
        public virtual void forEachRow(RowCallback<R, C, V> callback)
        {
            IEnumerator<R> iterator = this.matrix.Keys.GetEnumerator();
            while (iterator.MoveNext())
            {
                R row = iterator.Current;
                bool remove = callback.onRow(row, this.matrix[row]);
                if (remove)
                {
                    ConcurrentDictionary<C, V> rowMap;
                    this.matrix.TryRemove(row, out rowMap);
                }
            }
        }

        /// <summary>
        /// Executes a given callback passing each element of the specified row. The callback
        /// receives the element together with its coordinates. <BR>  
        /// Callbacks are executed synchronously before the method returns: calling 
        /// insert or delete methods during callback execution may result in 
        /// a wrong iteration, return false from the callback to remove the current element.
        /// </summary>
        public virtual void forEachElementInRow(R row, ElementCallback<R, C, V> callback)
        {
            ConcurrentDictionary<C, V> rowElements;
            matrix.TryGetValue(row, out rowElements);
            if (rowElements == null)
            {
                return;
            }

            IEnumerator<KeyValuePair<C, V>> iterator = rowElements.SetOfKeyValuePairs().GetEnumerator();
            while (iterator.MoveNext())
            {
                KeyValuePair<C, V> entry = iterator.Current;
                bool remove = callback.onElement(entry.Value, row, entry.Key);
                if (remove)
                {
                    rowElements.SetOfKeyValuePairs().Remove(entry);

                }
            }

            if (rowElements.Count == 0)
            {
                ConcurrentDictionary<C, V> rowMap;
                this.matrix.TryRemove(row, out rowMap);
            }
        }

        public virtual IList<V> sortAndCleanMatrix()
        {
            IList<V> sorted = new List<V>();

            SortedSet<R> rows = new SortedSet<R>(this.matrix.Keys);
            foreach (R row in rows)
            {
                ConcurrentDictionary<C, V> rowMap;
                matrix.TryGetValue(row, out rowMap);

                SortedSet<C> cols = new SortedSet<C>(rowMap.Keys);
                foreach (C col in cols)
                {
                    V envelope;
                    rowMap.TryGetValue(col, out envelope);
                    sorted.Add(envelope);
                }
            }

            this.matrix.Clear();
            return sorted;
        }

        public virtual void clear()
        {
            this.matrix.Clear();
        }

        public interface ElementCallback<R, C, V>
        {
            bool onElement(V value, R row, C col);
        }

        public interface RowCallback<R, C, V>
        {
            bool onRow(R row, ConcurrentDictionary<C, V> rowMap);
        }
    }
}