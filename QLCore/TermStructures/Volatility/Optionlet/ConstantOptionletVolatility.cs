/*
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

namespace QLCore
{
   //! Constant caplet volatility, no time-strike dependence
   public class ConstantOptionletVolatility : OptionletVolatilityStructure
   {
      private Handle<Quote> volatility_;

      //! floating reference date, floating market data
      public ConstantOptionletVolatility(int settlementDays, Calendar cal, BusinessDayConvention bdc,
                                         Handle<Quote> vol, DayCounter dc)
         : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = vol;
      }

      //! fixed reference date, floating market data
      public ConstantOptionletVolatility(Date referenceDate, Calendar cal, BusinessDayConvention bdc,
                                         Handle<Quote> vol, DayCounter dc)
         : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = vol;
      }

      //! floating reference date, fixed market data
      public ConstantOptionletVolatility(int settlementDays, Calendar cal, BusinessDayConvention bdc,
                                         double vol, DayCounter dc)
         : base(settlementDays, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(vol));
      }

      //! fixed reference date, fixed market data
      public ConstantOptionletVolatility(Date referenceDate, Calendar cal, BusinessDayConvention bdc,
                                         double vol, DayCounter dc)
         : base(referenceDate, cal, bdc, dc)
      {
         volatility_ = new Handle<Quote>(new SimpleQuote(vol));
      }


      public override Date maxDate() { return Date.maxDate(); }
      public override double minStrike() { return double.MinValue; }
      public override double maxStrike() { return double.MaxValue; }

      protected override SmileSection smileSectionImpl(Date d)
      {
         double atmVol = volatility_.link.value();
         return new FlatSmileSection(d, atmVol, dayCounter(), referenceDate());
      }

      protected override SmileSection smileSectionImpl(double optionTime)
      {
         double atmVol = volatility_.link.value();
         return new FlatSmileSection(optionTime, atmVol, dayCounter());
      }

      protected override double volatilityImpl(double d1, double d2)
      {
         return volatility_.link.value();
      }

   }
}
