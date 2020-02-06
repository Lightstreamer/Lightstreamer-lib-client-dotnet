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
    public class ConcurrentMatrix<R, C>
    {
        private ConcurrentDictionary<R, ConcurrentDictionary<C, string>> matrix = new ConcurrentDictionary<R, ConcurrentDictionary<C, string>>();
        private ConcurrentDictionary<C, string> t;
        internal readonly string NULL = "NULL";

        /// <summary>
        /// Inserts an element in the matrix. If another element is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insert(string value, R row, C column)
        {
            ConcurrentDictionary<C, string> matrixRow = null;

            matrix.TryGetValue(row, out matrixRow);

            if (matrixRow == null)
            {
                ConcurrentDictionary<C, string> newMatrixRow = new ConcurrentDictionary<C, string>();
                matrixRow = matrix.GetOrAdd(row, newMatrixRow);
                if (matrixRow == null)
                {
                    matrixRow = newMatrixRow;
                }
            }

            if (!string.ReferenceEquals(value, null))
            {
                matrixRow[column] = value;
            }
            else
            {
                matrixRow[column] = NULL;
            }
        }

        /// <summary>
        /// Gets the element at the specified position in the matrix. If the position is empty null is returned.
        /// </summary>
        public virtual string get(R row, C column)
        {
            ConcurrentDictionary<C, string> matrixRow = null;

            matrix.TryGetValue(row, out matrixRow);
            if (matrixRow != null)
            {
                string val;
                matrixRow.TryGetValue(column, out val);
                if (!string.ReferenceEquals(val, NULL))
                {
                    return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Removes the element at the specified position in the matrix.
        /// </summary>
        public virtual void del(R row, C column)
        {
            ConcurrentDictionary<C, string> matrixRow = null;

            matrix.TryGetValue(row, out matrixRow);
            if (matrixRow == null)
            {
                return;
            }
            string tmp;
            matrixRow.TryRemove(column, out tmp);
            if (matrixRow.IsEmpty)
            {

                //row is empty, get rid of it
                matrix.TryRemove(row, out t);
            }
        }

        /// <summary>
        /// Inserts a full row in the matrix. If another row is already present in the
        /// specified position it is overwritten.
        /// </summary>
        public virtual void insertRow(ConcurrentDictionary<C, string> insRow, R row)
        {
            matrix[row] = insRow;
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual ConcurrentDictionary<C, string> getRow(R row)
        {
            ConcurrentDictionary<C, string> matrixRow = null;

            matrix.TryGetValue(row, out matrixRow);
            return matrixRow;
        }

        /// <summary>
        /// Removes the row at the specified position in the matrix.
        /// </summary>
        public virtual void delRow(R row)
        {
            matrix.TryRemove(row, out t);
        }

        /// <summary>
        /// @deprecated
        /// </summary>
        public virtual ConcurrentDictionary<R, ConcurrentDictionary<C, string>> EntireMatrix
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
                return matrix.IsEmpty;
            }
        }

        /// <summary>
        /// Executes a given callback passing each element of the Matrix. The callback
        /// receives the element together with its coordinates. <BR>  
        /// Callbacks are executed synchronously before the method returns: calling 
        /// insert or delete methods during callback execution may result in 
        /// a wrong iteration, return false from the callback to remove the current element.
        /// </summary>
        public virtual void forEachElement(ElementCallback<R, C, string> callback)
        {
            //Iterator<String> iterator = list.iterator(); iterator.hasNext();
            foreach (R row in this.matrix.Keys)
            {
                this.forEachElementInRow(row, callback);
            }
        }

        /*
         * Executes a given callback passing the key of each row containing at least one element.
         */
        public virtual void forEachRow(RowCallback<R, C, string> callback)
        {
            IEnumerator<R> iterator = this.matrix.Keys.GetEnumerator();
            while (iterator.MoveNext())
            {
                R row = iterator.Current;
                bool remove = callback.onRow(row, this.matrix[row]);
                if (remove)
                {
                    this.matrix.Keys.Remove(row);
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
        public virtual void forEachElementInRow(R row, ElementCallback<R, C, string> callback)
        {
            ConcurrentDictionary<C, string> rowElements;
            matrix.TryGetValue(row, out rowElements);
            if (rowElements == null)
            {
                return;
            }

            IEnumerator<KeyValuePair<C, string>> iterator = rowElements.SetOfKeyValuePairs().GetEnumerator();
            while (iterator.MoveNext())
            {
                KeyValuePair<C, string> entry = iterator.Current;
                string value = entry.Value;
                if (string.ReferenceEquals(value, NULL))
                {
                    value = null;
                }
                bool remove = callback.onElement(value, row, entry.Key);
                if (remove)
                {
                    rowElements.SetOfKeyValuePairs().Remove(entry);
                }
            }

            if (rowElements.IsEmpty)
            {
                this.matrix.TryRemove(row, out t);
            }
        }

        public virtual IList<string> sortAndCleanMatrix()
        {
            LinkedList<string> sorted = new LinkedList<string>();

            SortedSet<R> rows = new SortedSet<R>(this.matrix.Keys);
            foreach (R row in rows)
            {
                ConcurrentDictionary<C, string> rowMap = this.matrix[row];
                SortedSet<C> cols = new SortedSet<C>(rowMap.Keys);
                foreach (C col in cols)
                {
                    string envelope = rowMap[col];
                    if (string.ReferenceEquals(envelope, NULL))
                    {
                        //sorted.AddLast(null);
                    }
                    else
                    {
                        sorted.AddLast(envelope);
                    }
                }
            }

            this.matrix.Clear();
            return (IList<string>)sorted;
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