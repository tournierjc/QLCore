﻿/*
 Copyright (C) 2020 Jean-Camille Tournier (mail@tournierjc.fr)

 This file is part of QLCore Project https://github.com/OpenDerivatives/QLCore

 QLCore is free software: you can redistribute it and/or modify it
 under the terms of the QLCore and QLNet license. You should have received a
 copy of the license along with this program; if not, license is
 available at https://github.com/OpenDerivatives/QLCore/LICENSE.

 QLCore is a forked of QLNet which is a based on QuantLib, a free-software/open-source
 library for financial quantitative analysts and developers - http://quantlib.org/
 The QuantLib license is available online at http://quantlib.org/license.shtml and the
 QLNet license is available online at https://github.com/amaggiulli/QLNet/blob/develop/LICENSE.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICAR PURPOSE. See the license for more details.
*/

using System;

namespace QLCore
{
   //! Implied vol term structure at a given date in the future
   /*! The given date will be the implied reference date.
       \note This term structure will remain linked to the original
             structure, i.e., any changes in the latter will be reflected
             in this structure as well.

       \warning It doesn't make financial sense to have an
                asset-dependant implied Vol Term Structure.  This
                class should be used with term structures that are
                time dependant only.
   */
   public class ImpliedVolTermStructure : BlackVarianceTermStructure
   {
      public ImpliedVolTermStructure(Handle<BlackVolTermStructure> originalTS, Date referenceDate)
         : base(referenceDate)
      {
         originalTS_ = originalTS;
      }
      // TermStructure interface
      public override DayCounter dayCounter() { return originalTS_.link.dayCounter(); }
      public override Date maxDate() { return originalTS_.link.maxDate(); }
      // VolatilityTermStructure interface
      public override double minStrike() { return originalTS_.link.minStrike(); }
      public override double maxStrike() { return originalTS_.link.maxStrike(); }
      // Visitability
      public virtual void accept(IAcyclicVisitor v)
      {
         if (v != null)
            v.visit(this);
         else
            Utils.QL_FAIL("not an event visitor");
      }
      protected override double blackVarianceImpl(double t, double strike)
      {
         /* timeShift (and/or variance) variance at evaluation date
           cannot be cached since the original curve could change
           between invocations of this method */
         double timeShift = dayCounter().yearFraction(originalTS_.link.referenceDate(), referenceDate());
         /* t is relative to the current reference date
            and needs to be converted to the time relative
            to the reference date of the original curve */
         return originalTS_.link.blackForwardVariance(timeShift, timeShift + t, strike, true);
      }

      private Handle<BlackVolTermStructure> originalTS_;

   }
}
