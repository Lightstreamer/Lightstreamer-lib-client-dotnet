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
        private Dictionary<R, Dictionary<C, V>> matrix = new Dictionary<R, Dictionary<C, V>>();

        /// <summary>
        /// Inserts an element in the matrix. If another element is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insert(V value, R row, C column)
        {
            Dictionary<C, V> matrixRow;
            bool presente = matrix.TryGetValue(row, out matrixRow);
            if (!presente)
            {
                matrixRow = new Dictionary<C, V>();
                matrix[row] = matrixRow;
            }

            matrixRow[column] = value;
        }

        /// <summary>
        /// Gets the element at the specified position in the matrix. If the position is empty null is returned.
        /// </summary>
        public virtual V get(R row, C column)
        {
            Dictionary<C, V> matrixRow;
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
            Dictionary<C, V> matrixRow;
            matrix.TryGetValue(row, out matrixRow);
            if (matrixRow == null)
            {
                return;
            }
            matrixRow.Remove(column);
            if (matrixRow.Count == 0)
            {
                //row is empty, get rid of it
                matrix.Remove(row);
            }
        }

        /// <summary>
        /// Inserts a full row in the matrix. If another row is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insertRow(Dictionary<C, V> insRow, R row)
        {
            matrix[row] = insRow;
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual Dictionary<C, V> getRow(R row)
        {
            Dictionary<C, V> rowi;
            matrix.TryGetValue(row, out rowi);

            return rowi;
        }

        /// <summary>
        /// Removes the row at the specified position in the matrix.
        /// </summary>
        public virtual void delRow(R row)
        {
            matrix.Remove(row);
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual Dictionary<R, Dictionary<C, V>> EntireMatrix
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
                    this.matrix.Remove(row);
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
            Dictionary<C, V> rowElements;
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
                this.matrix.Remove(row);
            }
        }

        public virtual IList<V> sortAndCleanMatrix()
        {
            IList<V> sorted = new List<V>();

            SortedSet<R> rows = new SortedSet<R>(this.matrix.Keys);
            foreach (R row in rows)
            {
                Dictionary<C, V> rowMap;
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
            bool onRow(R row, Dictionary<C, V> rowMap);
        }
    }
}