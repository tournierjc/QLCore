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
   //! base class for swap-rate indexes
   public class SwapIndex : InterestRateIndex
   {
      // need by CashFlowVectors
      public SwapIndex() { }


      public SwapIndex(string familyName,
                       Period tenor,
                       int settlementDays,
                       Currency currency,
                       Calendar calendar,
                       Period fixedLegTenor,
                       BusinessDayConvention fixedLegConvention,
                       DayCounter fixedLegDayCounter,
                       IborIndex iborIndex) :
         base(familyName, tenor, settlementDays, currency, calendar, fixedLegDayCounter)
      {
         tenor_ = tenor;
         iborIndex_ = iborIndex;
         fixedLegTenor_ = fixedLegTenor;
         fixedLegConvention_ = fixedLegConvention;
         exogenousDiscount_ = false;
         discount_ = new Handle<YieldTermStructure>();
      }

      public SwapIndex(string familyName,
                       Period tenor,
                       int settlementDays,
                       Currency currency,
                       Calendar calendar,
                       Period fixedLegTenor,
                       BusinessDayConvention fixedLegConvention,
                       DayCounter fixedLegDayCounter,
                       IborIndex iborIndex,
                       Handle<YieldTermStructure> discountingTermStructure) :
         base(familyName, tenor, settlementDays, currency, calendar, fixedLegDayCounter)
      {
         tenor_ = tenor;
         iborIndex_ = iborIndex;
         fixedLegTenor_ = fixedLegTenor;
         fixedLegConvention_ = fixedLegConvention;
         exogenousDiscount_ = true;
         discount_ = discountingTermStructure;
      }

      // InterestRateIndex interface
      public override Date maturityDate(Date valueDate)
      {
         Date fixDate = fixingDate(valueDate);
         return underlyingSwap(fixDate).maturityDate();
      }
      // Inspectors
      public Period fixedLegTenor() { return fixedLegTenor_; }
      public BusinessDayConvention fixedLegConvention() { return fixedLegConvention_; }
      public IborIndex iborIndex() { return iborIndex_; }
      public Handle<YieldTermStructure> forwardingTermStructure() { return iborIndex_.forwardingTermStructure(); }
      public Handle<YieldTermStructure> discountingTermStructure() { return discount_; }
      public bool exogenousDiscount() { return exogenousDiscount_; }
      // \warning Relinking the term structure underlying the index will not have effect on the returned swap.
      // recheck
      public VanillaSwap underlyingSwap(Date fixingDate)
      {
         Utils.QL_REQUIRE(fixingDate != null, () => "null fixing date");
         // caching mechanism
         if (lastFixingDate_ != fixingDate)
         {
            double fixedRate = 0.0;
            if (exogenousDiscount_)
               lastSwap_ = new MakeVanillaSwap(tenor_, iborIndex_, fixedRate)
               .withEffectiveDate(valueDate(fixingDate))
               .withFixedLegCalendar(fixingCalendar())
               .withFixedLegDayCount(dayCounter_)
               .withFixedLegTenor(fixedLegTenor_)
               .withFixedLegConvention(fixedLegConvention_)
               .withFixedLegTerminationDateConvention(fixedLegConvention_)
               .withDiscountingTermStructure(discount_)
               .value();
            else
               lastSwap_ = new MakeVanillaSwap(tenor_, iborIndex_, fixedRate)
               .withEffectiveDate(valueDate(fixingDate))
               .withFixedLegCalendar(fixingCalendar())
               .withFixedLegDayCount(dayCounter_)
               .withFixedLegTenor(fixedLegTenor_)
               .withFixedLegConvention(fixedLegConvention_)
               .withFixedLegTerminationDateConvention(fixedLegConvention_)
               .value();
            lastFixingDate_ = fixingDate;
         }
         return lastSwap_;
      }
      // Other methods
      // returns a copy of itself linked to a different forwarding curve
      public virtual SwapIndex clone(Handle<YieldTermStructure> forwarding)
      {
         SwapIndex tmp;
         if (exogenousDiscount_)
            tmp = new SwapIndex(familyName(),
                                 tenor(),
                                 fixingDays(),
                                 currency(),
                                 fixingCalendar(),
                                 fixedLegTenor(),
                                 fixedLegConvention(),
                                 dayCounter(),
                                 iborIndex_.clone(forwarding),
                                 discount_);
         else
            tmp = new SwapIndex(familyName(),
                                 tenor(),
                                 fixingDays(),
                                 currency(),
                                 fixingCalendar(),
                                 fixedLegTenor(),
                                 fixedLegConvention(),
                                 dayCounter(),
                                 iborIndex_.clone(forwarding));
         
         tmp.data_ = data_;
         return tmp;
      }
      //! returns a copy of itself linked to a different curves
      public virtual SwapIndex clone(Handle<YieldTermStructure> forwarding, Handle<YieldTermStructure> discounting)
      {
         SwapIndex tmp = new SwapIndex(familyName(),
                                       tenor(),
                                       fixingDays(),
                                       currency(),
                                       fixingCalendar(),
                                       fixedLegTenor(),
                                       fixedLegConvention(),
                                       dayCounter(),
                                       iborIndex_.clone(forwarding),
                                       discounting);
         tmp.data_ = data_;
         return tmp;
      }
      //! returns a copy of itself linked to a different tenor
      public virtual SwapIndex clone(Period tenor)
      {
         SwapIndex tmp;
         if (exogenousDiscount_)
            tmp = new SwapIndex(familyName(),
                                 tenor,
                                 fixingDays(),
                                 currency(),
                                 fixingCalendar(),
                                 fixedLegTenor(),
                                 fixedLegConvention(),
                                 dayCounter(),
                                 iborIndex(),
                                 discountingTermStructure());
         else
            tmp = new SwapIndex(familyName(),
                                 tenor,
                                 fixingDays(),
                                 currency(),
                                 fixingCalendar(),
                                 fixedLegTenor(),
                                 fixedLegConvention(),
                                 dayCounter(),
                                 iborIndex());
         
         tmp.data_ = data_;
         return tmp;
      }
      public override double forecastFixing(Date fixingDate)
      {
         return underlyingSwap(fixingDate).fairRate();
      }

      protected new Period tenor_;
      protected IborIndex iborIndex_;
      protected Period fixedLegTenor_;
      protected BusinessDayConvention fixedLegConvention_;
      protected bool exogenousDiscount_;
      protected  Handle<YieldTermStructure> discount_;
      // cache data to avoid swap recreation when the same fixing date
      // is used multiple time to forecast changing fixing
      protected VanillaSwap lastSwap_;
      protected Date lastFixingDate_;
   }

   //! base class for overnight indexed swap indexes
   public class OvernightIndexedSwapIndex : SwapIndex
   {
      public OvernightIndexedSwapIndex(string familyName,
                                       Period tenor,
                                       int settlementDays,
                                       Currency currency,
                                       OvernightIndex overnightIndex)
         : base(familyName, tenor, settlementDays,
                currency, overnightIndex.fixingCalendar(),
                new Period(1, TimeUnit.Years), BusinessDayConvention.ModifiedFollowing,
                overnightIndex.dayCounter(), overnightIndex)
      {
         overnightIndex_ = overnightIndex;
      }
      // Inspectors
      public OvernightIndex overnightIndex() { return overnightIndex_; }
      /*! \warning Relinking the term structure underlying the index will
                   not have effect on the returned swap.
      */
      public new OvernightIndexedSwap underlyingSwap(Date fixingDate)
      {
         Utils.QL_REQUIRE(fixingDate != null, () => "null fixing date");
         if (lastFixingDate_ != fixingDate)
         {
            double fixedRate = 0.0;
            lastSwap_ = new MakeOIS(tenor_, overnightIndex_, fixedRate)
            .withEffectiveDate(valueDate(fixingDate))
            .withFixedLegDayCount(dayCounter_);
            lastFixingDate_ = fixingDate;
         }
         return lastSwap_;
      }

      protected OvernightIndex overnightIndex_;
      // cache data to avoid swap recreation when the same fixing date
      // is used multiple time to forecast changing fixing
      protected new OvernightIndexedSwap lastSwap_;
      protected new Date lastFixingDate_;
   }


}
