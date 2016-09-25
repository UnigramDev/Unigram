/********************************************************************************
 *   This file is part of NRtfTree Library.
 *
 *   NRtfTree Library is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU Lesser General Public License as published by
 *   the Free Software Foundation; either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   NRtfTree Library is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU Lesser General Public License for more details.
 *
 *   You should have received a copy of the GNU Lesser General Public License
 *   along with this program. If not, see <http://www.gnu.org/licenses/>.
 ********************************************************************************/

/********************************************************************************
 * Library:		NRtfTree
 * Version:     v0.4
 * Date:		29/06/2013
 * Copyright:   2006-2013 Salvador Gomez
 * Home Page:	http://www.sgoliver.net
 * GitHub:	    https://github.com/sgolivernet/nrtftree
 * Class:		RtfColorTable
 * Description:	Tabla de Colores de un documento RTF.
 * ******************************************************************************/

using System.Collections.Generic;
using Windows.UI;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Tabla de colores de un documento RTF.
    /// </summary>
    public class RtfColorTable
    {
        /// <summary>
        /// Lista interna de colores.
        /// </summary>
        private List<Color> colors;

        /// <summary>
        /// Constructor de la clase RtfColorTable.
        /// </summary>
        public RtfColorTable()
        {
            colors = new List<Color>();
        }

        /// <summary>
        /// Inserta un nuevo color en la tabla de colores.
        /// </summary>
        /// <param name="color">Nuevo color a insertar.</param>
        public void AddColor(Color color)
        {
            colors.Add(color);
        }

        /// <summary>
        /// Obtiene el color n-ésimo de la tabla de colores.
        /// </summary>
        /// <param name="index">Indice del color a recuperar.</param>
        /// <returns>Color n-ésimo de la tabla de colores.</returns>
        public Color this[int index]
        {
            get
            {
                return colors[index];
            }
        }

        /// <summary>
        /// Número de colores en la tabla.
        /// </summary>
        public int Count
        {
            get
            {
                return colors.Count;
            }
        }

        /// <summary>
        /// Obtiene el índice de un color determinado en la tabla.
        /// </summary>
        /// <param name="color">Color a consultar.</param>
        /// <returns>Indice del color consultado.</returns>
        public int IndexOf(Color color)
        {
            return colors.IndexOf(color);
        }
    }
}
