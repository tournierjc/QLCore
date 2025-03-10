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
using System.Collections.Generic;
using System.Linq;

namespace QLCore
{
   public class FdmAffineModelTermStructure : YieldTermStructure
   {
      public FdmAffineModelTermStructure(
         Vector r,
         Calendar cal,
         DayCounter dayCounter,
         Date referenceDate,
         Date modelReferenceDate,
         IAffineModel model)
         : base(referenceDate, cal, dayCounter)
      {
         r_ = r;
         t_ = dayCounter.yearFraction(modelReferenceDate, referenceDate);
         model_ = model;
      }

      public override Date maxDate() { return Date.maxDate(); }
      public void setVariable(Vector r)
      {
         r_ = r;
      }

      protected internal override double discountImpl(double d)
      {
         return model_.discountBond(t_, d + t_, r_);
      }

      protected Vector r_;
      protected double t_;
      protected IAffineModel model_;
   }
}
